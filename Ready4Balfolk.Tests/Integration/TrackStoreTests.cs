using System.Diagnostics;
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

namespace Ready4Balfolk.Tests.Integration;

public sealed class TrackStoreTests : IDisposable
{
    private readonly DirectoryInfo _tempDirA;
    private readonly DirectoryInfo _tempDirB;
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

    public TrackStoreTests()
    {
        SupportedAudioFormats.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3" });

        _tempDirA = CreateTempDirectory();
        _tempDirB = CreateTempDirectory();

        _loggerService = Substitute.For<ILoggerService>();

        _discoveryService = Substitute.For<ITrackDiscoveryService>();
        var discoveryService = _discoveryService;
        discoveryService.Gather(Arg.Any<FileInfo>(), Arg.Any<DirectoryInfo>())
            .Returns(call => EvidenceFor(call.Arg<FileInfo>()!));

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
        _libraryIndex.ApprovalsAsync(Arg.Any<CancellationToken>()).Returns(_ => Approved());

        _sut = new TrackStore(_loggerService, discoveryService, danceListStore, _libraryIndex);
    }

    [Fact]
    public async Task MusicDirectory_ChangedTwice_ReloadsAndKeepsWatching()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "b.mp3"), "", TestContext.Current.CancellationToken);

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "a.mp3"));

        // Switching a second time used to throw a NullReferenceException from the
        // watcher remove-handlers and leave the store without a FileSystemWatcher.
        _sut.MusicDirectory = _tempDirB;
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));
        await WaitUntilAsync(() => !isLoading);

        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");

        // The watcher must be re-attached to the new directory: a file created
        // after the switch has to show up in the store.
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "c.mp3"), "", TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(() =>
            {
                lock (_indexSnapshot)
                {
                    return _indexSnapshot.Keys.Any(path => path.EndsWith("c.mp3", StringComparison.Ordinal));
                }
            });
        }
        catch (Exception)
        {
            var messages = string.Join(" | ", _loggerService.ReceivedCalls()
                .Select(call => call.GetMethodInfo().Name + ":" + string.Join(",", call.GetArguments().Take(1))));
            throw new InvalidOperationException("no c.mp3 indexed. log: " + messages);
        }
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    [Fact]
    public async Task MusicDirectory_MissingDirectory_LogsWarningAndDoesNotStickLoading()
    {
        var missing = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), $"r4b_missing_{Guid.NewGuid():N}"));

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        _sut.MusicDirectory = missing;

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
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "Naragonia - Mazurka.mp3"), "", TestContext.Current.CancellationToken);

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

        _sut.DiscoverySettings = new DiscoverySettings { FileNamePatterns = ["%a - %t"] };
        _sut.MusicDirectory = _tempDirA;

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
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Any(track => track.FileInfo.Name == "a.mp3"));

        await _libraryIndex.DidNotReceive().ApproveAsync(
            Arg.Is<IReadOnlyCollection<TrackApproval>>(approvals => approvals != null && approvals.Count > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangingTheRules_TakesBackWhatTheyApproved()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "Naragonia - Mazurka.mp3"), "", TestContext.Current.CancellationToken);

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        _sut.DiscoverySettings = new DiscoverySettings { FileNamePatterns = ["%a - %t"] };

        await WaitUntilAsync(() => _libraryIndex.ReceivedCalls()
            .Any(call => call.GetMethodInfo().Name == nameof(ILibraryIndex.RevokeRuleApprovalsAsync)));
    }

    [Fact]
    public async Task AReimportedDanceList_LetsTheTracksItNowCarriesThrough()
    {
        // A merged proposal at BigBalfolkList has to visibly clear part of the backlog. The track
        // was answered long ago; only the list was missing the name.
        _danceIndex = DanceListIndex.Empty;
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);

        var isLoading = true;
        using var loading = _sut.IsLoading.Subscribe(value => isLoading = value);

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _indexSnapshot.Count == 1 && !isLoading);

        // Approved on every field, and still not in the library: the list has never heard of the
        // dance, so the gate holds it.
        Assert.Empty(_sut.Current);

        var carried = new DanceList { Dances = [TestData.CreateDance("mazurka", names: ["Mazurka"])] };
        _danceIndex = DanceListIndex.Build(carried);
        _danceLists.OnNext(carried);

        await WaitUntilAsync(() => _sut.Current.Count == 1);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try
        {
            _tempDirA.Delete(true);
            _tempDirB.Delete(true);
        }
        catch
        {
            // cleanup best-effort
        }
    }

    private static DirectoryInfo CreateTempDirectory()
    {
        var directory = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        directory.Create();
        return directory;
    }

    [Fact]
    public async Task UnchangedFile_IsNotOpened()
    {
        var path = Path.Combine(_tempDirA.FullName, "known.mp3");
        await File.WriteAllTextAsync(path, "audio", TestContext.Current.CancellationToken);
        var file = new FileInfo(path);
        _indexSnapshot = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [path] = IndexedAs(file, "Mazurka")
        };

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        // The whole point of the index: a startup that finds nothing changed touches no audio.
        _discoveryService.DidNotReceiveWithAnyArgs().Gather(default!, default!);
        Assert.Equal("Mazurka", _sut.Current[0].Dance);
        Assert.Equal(TimeSpan.FromSeconds(42), _sut.Current[0].Length);
    }

    [Fact]
    public async Task ChangedFile_IsReadAgain()
    {
        var path = Path.Combine(_tempDirA.FullName, "changed.mp3");
        await File.WriteAllTextAsync(path, "audio", TestContext.Current.CancellationToken);
        var file = new FileInfo(path);
        _indexSnapshot = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            // A size the file no longer has, which is what "this changed" looks like without
            // opening it.
            [path] = IndexedAs(file, "Mazurka") with { FileSize = file.Length + 1 }
        };

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        _discoveryService.ReceivedWithAnyArgs().Gather(default!, default!);
    }

    [Fact]
    public async Task FileTheIndexHasNeverSeen_IsReadAndWrittenBack()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "fresh.mp3"), "audio", TestContext.Current.CancellationToken);

        _sut.MusicDirectory = _tempDirA;
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

    private static LibraryEntry IndexedAs(FileInfo file, string dance) => new()
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

    private static TrackEvidence EvidenceFor(FileInfo fileInfo) => new()
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
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "b.mp3"), "", TestContext.Current.CancellationToken);

        // Switch without waiting, so the second load starts while the first is still running.
        // Unserialised, the two race over _watcher and _tracks: one disposes the watcher the
        // other just published, and enabling it then throws ObjectDisposedException.
        _sut.MusicDirectory = _tempDirA;
        _sut.MusicDirectory = _tempDirB;

        var isLoading = true;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));
        await WaitUntilAsync(() => !isLoading);

        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");

        // The surviving load must still own a live watcher on its own directory.
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "c.mp3"), "", TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10_000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
                "Timed out waiting for condition");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
