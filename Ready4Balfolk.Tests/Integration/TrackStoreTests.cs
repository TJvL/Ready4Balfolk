using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class TrackStoreTests : IDisposable
{
    private readonly WatchableMockFileSystem _fileSystem;
    // Every watcher the store asked for, latest last. The store's watcher is a substitute, so a
    // "file appeared" is an event the test raises rather than a real FileSystemWatcher noticing a
    // real write: nothing here touches the filesystem, and nothing waits on it either.
    private readonly List<(string Path, IFileSystemWatcher Watcher)> _watchers = [];
    private readonly IDirectoryInfo _dirA;
    private readonly IDirectoryInfo _dirB;
    private readonly ILoggerService _loggerService;
    private readonly ITrackDiscoveryService _discoveryService;
    private readonly ILibraryIndex _libraryIndex;
    private Dictionary<string, LibraryEntry> _indexSnapshot = [];
    // A BehaviorSubject as the real store is: it replays its current list to a new subscriber, and
    // the store's Skip(1) is there to drop exactly that replay. A bare Subject makes the first real
    // update look like the replay and it is silently swallowed.
    private readonly BehaviorSubject<DanceList> _danceLists;
    private DanceListIndex _danceIndex = DanceListIndex.Empty;
    private readonly TrackStore _sut;
    private TrackLibraryConfiguration _configuration = TrackLibraryConfiguration.Undeclared;

    public TrackStoreTests()
    {
        SupportedAudioFormats.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3" });

        _fileSystem = new WatchableMockFileSystem(CreateWatcher);
        _dirA = _fileSystem.DirectoryInfo.New("/music/a");
        _dirA.Create();
        _dirB = _fileSystem.DirectoryInfo.New("/music/b");
        _dirB.Create();

        _loggerService = Substitute.For<ILoggerService>();

        _discoveryService = Substitute.For<ITrackDiscoveryService>();
        var discoveryService = _discoveryService;
        discoveryService.Gather(Arg.Any<IFileInfo>(), Arg.Any<IDirectoryInfo>())
            .Returns(call => EvidenceFor(call.Arg<IFileInfo>()!));

        // A list with the one dance these tests use, because a track only reaches the library with a
        // dance the published list knows.
        var danceList = new DanceList { Dances = [TestData.CreateDance("mazurka", names: ["Mazurka"])] };
        _danceIndex = DanceListIndex.Build(danceList);
        _danceLists = new BehaviorSubject<DanceList>(danceList);

        var danceListStore = Substitute.For<IDanceListStore>();
        danceListStore.Current.Returns(danceList);
        danceListStore.Index.Returns(_ => _danceIndex);
        danceListStore.Observe().Returns(_danceLists);

        // An index that remembers what it is written, and says every track was approved. These
        // tests are about the store's discovery and watching; the gate has its own.
        _libraryIndex = Substitute.For<ILibraryIndex>();
        // A copy, as the real one hands back, so a scan reading it cannot trip over a watcher
        // writing to it.
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lock (_indexSnapshot)
                {
                    return new Dictionary<string, LibraryEntry>(_indexSnapshot, StringComparer.Ordinal);
                }
            });
        _libraryIndex.WriteAsync(Arg.Any<IReadOnlyCollection<LibraryEntry>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                lock (_indexSnapshot)
                {
                    foreach (var entry in call.Arg<IReadOnlyCollection<LibraryEntry>>()!)
                    {
                        _indexSnapshot[entry.Path] = entry;
                    }
                }

                return Task.CompletedTask;
            });
        _libraryIndex.DeleteMissingAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var present = call.Arg<IReadOnlyCollection<string>>()!.ToHashSet(StringComparer.Ordinal);
                lock (_indexSnapshot)
                {
                    foreach (var gone in _indexSnapshot.Keys.Where(path => !present.Contains(path)).ToList())
                    {
                        _indexSnapshot.Remove(gone);
                    }
                }

                return Task.CompletedTask;
            });
        _libraryIndex.DeletePathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                lock (_indexSnapshot)
                {
                    _indexSnapshot.Remove(call.Arg<string>()!);
                }

                return Task.CompletedTask;
            });
        _libraryIndex.ApprovalsAsync(Arg.Any<CancellationToken>()).Returns(_ => Approved());

        _sut = new TrackStore(_loggerService, discoveryService, danceListStore, _libraryIndex, _fileSystem);
    }

    private IFileSystemWatcher CreateWatcher(string path)
    {
        var watcher = Substitute.For<IFileSystemWatcher>();
        watcher.Path.Returns(path);
        lock (_watchers)
        {
            _watchers.Add((path, watcher));
        }

        return watcher;
    }

    private void CreateFile(IDirectoryInfo directory, string name) =>
        _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(directory.FullName, name), "audio");

    /// <summary>Writes a file and raises Created on the store's current watcher for its directory.</summary>
    private void CreateFileAndNotify(IDirectoryInfo directory, string name)
    {
        CreateFile(directory, name);

        IFileSystemWatcher watcher;
        lock (_watchers)
        {
            watcher = _watchers.Last(w => string.Equals(w.Path, directory.FullName, StringComparison.Ordinal)).Watcher;
        }

        watcher.Created += Raise.Event<FileSystemEventHandler>(
            watcher, new FileSystemEventArgs(WatcherChangeTypes.Created, directory.FullName, name));
    }

    [Fact]
    public async Task MusicDirectory_ChangedTwice_ReloadsAndKeepsWatching()
    {
        CreateFile(_dirA, "a.mp3");
        CreateFile(_dirB, "b.mp3");

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "a.mp3"));

        // Switching a second time used to throw a NullReferenceException from the
        // watcher remove-handlers and leave the store without a watcher.
        await ApplyAsync(directory: _dirB);
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));
        await WaitUntilAsync(() => !isLoading);

        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");

        // The watcher must be re-attached to the new directory: a file created
        // after the switch has to show up in the store.
        CreateFileAndNotify(_dirB, "c.mp3");
        await WaitUntilAsync(() =>
        {
            lock (_indexSnapshot)
            {
                return _indexSnapshot.Keys.Any(path => path.EndsWith("c.mp3", StringComparison.Ordinal));
            }
        });
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    [Fact]
    public async Task ADeletedFile_LeavesTheIndexAsWellAsTheLibrary()
    {
        // Without the index delete the next rebuild resurrects the file: any refresh republishes
        // whatever rows the index still holds.
        CreateFile(_dirA, "a.mp3");
        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        _fileSystem.File.Delete(_fileSystem.Path.Combine(_dirA.FullName, "a.mp3"));
        IFileSystemWatcher watcher;
        lock (_watchers)
        {
            watcher = _watchers.Last(w => string.Equals(w.Path, _dirA.FullName, StringComparison.Ordinal)).Watcher;
        }

        watcher.Deleted += Raise.Event<FileSystemEventHandler>(
            watcher, new FileSystemEventArgs(WatcherChangeTypes.Deleted, _dirA.FullName, "a.mp3"));

        await WaitUntilAsync(() =>
        {
            lock (_indexSnapshot)
            {
                return _indexSnapshot.Count == 0;
            }
        });
        await WaitUntilAsync(() => _sut.Current.Count == 0);
    }

    [Fact]
    public async Task MusicDirectory_MissingDirectory_LogsWarningAndDoesNotStickLoading()
    {
        var missing = _fileSystem.DirectoryInfo.New("/music/missing");

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        await ApplyAsync(directory: missing);

        await WaitUntilAsync(() => _loggerService.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(ILoggerService.WarningAsync)));
        Assert.False(isLoading);
        Assert.Empty(_sut.Current);
    }

    [Fact]
    public async Task ADeclaredRule_ApprovesEveryFileItAnswers()
    {
        // The bargain of a declaration: the user vouches for the rule once, and the files it matches
        // are answered and approved without being looked at one at a time.
        CreateFile(_dirA, "Naragonia - Mazurka.mp3");

        var approved = new List<TrackApproval>();
        _libraryIndex.ApproveAsync(Arg.Any<IReadOnlyCollection<TrackApproval>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                lock (approved)
                {
                    approved.AddRange(call.Arg<IReadOnlyCollection<TrackApproval>>()!);
                }

                return Task.CompletedTask;
            });

        await ApplyAsync(discovery: new DiscoverySettings { FileNamePatterns = ["%a - %t"] });
        await ApplyAsync(directory: _dirA);

        await WaitUntilAsync(() =>
        {
            lock (approved)
            {
                return approved.Count >= 2;
            }
        });

        lock (approved)
        {
            Assert.Contains(approved, approval =>
                approval.Field == TrackField.Artist
                && approval.Value == "Naragonia"
                && approval.Kind == ApprovalKind.ByRule
                && approval.Rule == "%a - %t");
            Assert.Contains(approved, approval => approval.Field == TrackField.Title && approval.Value == "Mazurka");
        }
    }

    [Fact]
    public async Task WhatNoRuleAnswered_IsApprovedByNothing()
    {
        CreateFile(_dirA, "a.mp3");

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Any(track => track.FileInfo.Name == "a.mp3"));

        await _libraryIndex.DidNotReceive().ApproveAsync(
            Arg.Is<IReadOnlyCollection<TrackApproval>>(approvals => approvals != null && approvals.Count > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangingTheRules_TakesBackWhatTheyApproved()
    {
        CreateFile(_dirA, "Naragonia - Mazurka.mp3");

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        await ApplyAsync(discovery: new DiscoverySettings { FileNamePatterns = ["%a - %t"] });

        await WaitUntilAsync(() => _libraryIndex.ReceivedCalls()
            .Any(call => call.GetMethodInfo().Name == nameof(ILibraryIndex.RevokeRuleApprovalsAsync)));
    }

    [Fact]
    public async Task AReimportedDanceList_LetsTheTracksItNowCarriesThrough()
    {
        // A merged proposal at BigBalfolkList has to visibly clear part of the backlog. The track
        // was answered long ago; only the list was missing the name.
        _danceIndex = DanceListIndex.Empty;
        CreateFile(_dirA, "a.mp3");

        var isLoading = true;
        using var loading = _sut.IsLoading.Subscribe(value => isLoading = value);

        var inReview = 0;
        using var counting = _sut.InReviewCount.Subscribe(count => inReview = count);

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _indexSnapshot.Count == 1 && !isLoading);

        // Approved on every field, and still not in the library: the list has never heard of the
        // dance, so the gate holds it. The badge count has to say so too: a SQL count keyed on
        // "fewer than three approvals" once reported zero here, and the badge lied.
        Assert.Empty(_sut.Current);
        Assert.Equal(1, inReview);

        var carried = new DanceList { Dances = [TestData.CreateDance("mazurka", names: ["Mazurka"])] };
        _danceIndex = DanceListIndex.Build(carried);
        _danceLists.OnNext(carried);

        await WaitUntilAsync(() => _sut.Current.Count == 1);
        Assert.Equal(0, inReview);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task UnchangedFile_IsNotOpened()
    {
        CreateFile(_dirA, "known.mp3");
        var file = _fileSystem.FileInfo.New(_fileSystem.Path.Combine(_dirA.FullName, "known.mp3"));
        _indexSnapshot = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [file.FullName] = IndexedAs(file, "Mazurka")
        };

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        // The whole point of the index: a startup that finds nothing changed touches no audio.
        _discoveryService.DidNotReceiveWithAnyArgs().Gather(default!, default!);
        Assert.Equal("Mazurka", _sut.Current[0].Dance);
        Assert.Equal(TimeSpan.FromSeconds(42), _sut.Current[0].Length);
    }

    [Fact]
    public async Task ChangedFile_IsReadAgain()
    {
        CreateFile(_dirA, "changed.mp3");
        var file = _fileSystem.FileInfo.New(_fileSystem.Path.Combine(_dirA.FullName, "changed.mp3"));
        _indexSnapshot = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            // A size the file no longer has, which is what "this changed" looks like without
            // opening it.
            [file.FullName] = IndexedAs(file, "Mazurka") with { FileSize = file.Length + 1 }
        };

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        _discoveryService.ReceivedWithAnyArgs().Gather(default!, default!);
    }

    [Fact]
    public async Task FileTheIndexHasNeverSeen_IsReadAndWrittenBack()
    {
        CreateFile(_dirA, "fresh.mp3");

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        await _libraryIndex.ReceivedWithAnyArgs().WriteAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>Every indexed track, answered on all three fields, so the gate is not the subject.</summary>
    private Dictionary<string, IReadOnlyList<TrackApproval>> Approved()
    {
        lock (_indexSnapshot)
        {
            return _indexSnapshot.Values
                .GroupBy(entry => LibraryKey.For(entry.ContentHash), StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<TrackApproval>)
                    [
                        Approval(group.First(), TrackField.Dance, "Mazurka"),
                        Approval(group.First(), TrackField.Artist, Or(group.First().Artist, "Artist")),
                        Approval(group.First(), TrackField.Title, Or(group.First().Title, "Title"))
                    ],
                    StringComparer.Ordinal);
        }
    }

    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static TrackApproval Approval(LibraryEntry entry, TrackField field, string value) => new()
    {
        ContentHash = entry.ContentHash,
        Field = field,
        Value = value,
        Kind = ApprovalKind.Individual,
        FileWriteUtc = entry.LastWriteUtc
    };

    private static LibraryEntry IndexedAs(IFileInfo file, string dance) => new()
    {
        ContentHash = [7],
        Path = file.FullName,
        FileSize = file.Length,
        LastWriteUtc = file.LastWriteTimeUtc,
        Duration = TimeSpan.FromSeconds(42),
        Format = AudioFormat.Mp3,
        DanceSlug = null,
        OriginalDance = dance,
        Artist = "Artist",
        Title = file.Name
    };

    private static TrackEvidence EvidenceFor(IFileInfo fileInfo) => new()
    {
        FileName = fileInfo.Name,
        PathSegments = ["Artist"],
        Duration = TimeSpan.FromSeconds(180),
        Format = AudioFormat.Mp3,
        // Distinct per file, as a real content hash is: sharing one hash would make every file the
        // same recording, and an approval of one an approval of all.
        ContentHash = System.Text.Encoding.UTF8.GetBytes(fileInfo.Name)
    };

    [Fact]
    public async Task MusicDirectory_SwitchedWhileLoading_SerialisesAndKeepsWatching()
    {
        CreateFile(_dirA, "a.mp3");
        CreateFile(_dirB, "b.mp3");

        // Switch without waiting, so the second load starts while the first is still running.
        // Unserialised, the two race over _watcher and _tracks: one disposes the watcher the
        // other just published, and enabling it then throws ObjectDisposedException.
        await ApplyAsync(directory: _dirA);
        await ApplyAsync(directory: _dirB);

        var isLoading = true;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));
        await WaitUntilAsync(() => !isLoading);

        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");

        // The surviving load must still own a live watcher on its own directory.
        CreateFileAndNotify(_dirB, "c.mp3");
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    /// <summary>
    /// A rebuild asked for while a load is running has to wait for it, not run through it.
    /// </summary>
    /// <remarks>
    /// Both clear the published list and refill it, so overlapping them leaves the library holding
    /// whichever finished writing last. This is the review screen approving a track during a scan.
    /// </remarks>
    [Fact]
    public async Task RefreshLibrary_DuringALoad_WaitsForItRatherThanInterleaving()
    {
        CreateFile(_dirA, "a.mp3");

        // Held open inside the load, which by then is inside the gate. Whether the rebuild is
        // serialised is exactly the question of whether it can get past this while the load holds it.
        var loadIsInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTheLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rebuildRan = false;

        _libraryIndex.DeleteMissingAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                loadIsInside.TrySetResult();
                await releaseTheLoad.Task;
            });

        // Not awaited: the point is to ask for a rebuild while this load is still inside the gate.
        var load = ApplyAsync(directory: _dirA);
        await loadIsInside.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var rebuild = Task.Run(async () =>
        {
            await _sut.RefreshLibraryAsync(TestContext.Current.CancellationToken);
            rebuildRan = true;
        }, TestContext.Current.CancellationToken);

        // Long enough that an ungated rebuild would have finished: it opens no files and reads one
        // in-memory snapshot. Before the gate, this assertion failed.
        await Task.Delay(250, TestContext.Current.CancellationToken);
        Assert.False(rebuildRan, "the rebuild ran while the load still held the gate");

        releaseTheLoad.TrySetResult();
        await load.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await rebuild.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(rebuildRan);
        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
    }

    /// <summary>
    /// A directory change arriving behind a rule toggle still wins, and logs nothing.
    /// </summary>
    /// <remarks>
    /// A guard rather than a regression test: the toggle's rebuild now runs under the current load
    /// token, and this is what must stay true whether it is dropped or merely serialised behind the
    /// load. Whether it was in fact dropped is not observable from out here.
    /// </remarks>
    [Fact]
    public async Task AllowDancesOutsideTheList_FollowedByADirectoryChange_TheDirectoryChangeWins()
    {
        CreateFile(_dirA, "a.mp3");
        CreateFile(_dirB, "b.mp3");

        await ApplyAsync(directory: _dirA);
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name is "a.mp3" or "b.mp3"));

        // Started together and deliberately not awaited in order: the directory change has to be
        // able to supersede the rebuild the rule toggle asked for.
        var toggled = ApplyAsync(allowOutside: true);
        var moved = ApplyAsync(directory: _dirB);
        await Task.WhenAll(toggled, moved);

        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));
        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");
    }

    /// <summary>Applies a change to one part of the configuration, keeping the rest.</summary>
    /// <remarks>
    /// The store takes all three together now, so a test that wants to move one has to say what the
    /// other two still are. Returns the task rather than awaiting, because a couple of these tests
    /// are about what happens while a load is still running.
    /// </remarks>
    private Task ApplyAsync(
        IDirectoryInfo? directory = null, DiscoverySettings? discovery = null, bool? allowOutside = null)
    {
        _configuration = _configuration with
        {
            MusicDirectoryPath = directory?.FullName ?? _configuration.MusicDirectoryPath,
            Discovery = discovery ?? _configuration.Discovery,
            AllowDancesOutsideTheList = allowOutside ?? _configuration.AllowDancesOutsideTheList
        };

        return _sut.ApplyAsync(_configuration);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10_000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
                "Timed out waiting for condition");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
