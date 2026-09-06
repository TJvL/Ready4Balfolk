using System.Globalization;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class QueueServiceTests : IDisposable
{
    private readonly QueueService _sut;
    private readonly BehaviorSubject<ApplicationSettings> _settingsSubject;
    private readonly BehaviorSubject<QueueHistory> _historySubject;
    private readonly Subject<string> _vanished;
    private readonly Subject<PathMove> _moved;
    private Func<IQueueItem?> _currentItem = () => null;

    public QueueServiceTests()
    {
        var settings = new ApplicationSettings() with
        {
            MaxQueueItems = 100
        };
        _settingsSubject = new BehaviorSubject<ApplicationSettings>(settings);

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_ => _settingsSubject.Value);
        settingsStore.Observe().Returns(_settingsSubject);

        _historySubject = new BehaviorSubject<QueueHistory>(new QueueHistory(null, []));
        var historyStore = Substitute.For<IQueueHistoryStore>();
        historyStore.Current.Returns(_ => _historySubject.Value);
        historyStore.Observe().Returns(_historySubject);

        _vanished = new Subject<string>();
        _moved = new Subject<PathMove>();
        var trackStore = Substitute.For<ITrackStore>();
        trackStore.WhenTrackFileVanished.Returns(_vanished);
        trackStore.WhenTrackFileMoved.Returns(_moved);

        _sut = new QueueService(
            settingsStore, historyStore, trackStore, () => _currentItem(), () => TimeSpan.Zero,
            new NoOpLoggerService(),
            TimeProvider.System);
    }

    // --- What has gone from the disk ---

    [Fact]
    public void ATrackWhoseFileHasGone_LeavesTheQueue()
    {
        var track = TestData.CreateTrack();
        _sut.Enqueue(new TrackQueueItem(track, false));
        _sut.Enqueue(new StopQueueItem());

        _vanished.OnNext(track.FileInfo.FullName);

        // A queued dance whose file has gone can never play, and finding that out when the room is
        // waiting for it is the worst moment to find it out.
        Assert.Equal(1, _sut.Count);
        Assert.IsType<StopQueueItem>(_sut.Items[0]);
    }

    [Fact]
    public void ATrackWhoseFileMoved_StaysInTheQueuePointingAtWhereItWent()
    {
        // The DJ tidies a folder up mid-evening. The library follows the files, and the queue was
        // handed its track when the request was made, so without being told it keeps a path that
        // is not there and the room finds out when it is that track's turn.
        var track = TestData.CreateTrack();
        var queued = new TrackQueueItem(track, false);
        _sut.Enqueue(queued);
        _sut.Enqueue(new StopQueueItem());

        var moved = TidiedInto(track, "Naragonia");
        _moved.OnNext(new PathMove(track.FileInfo.FullName, moved));

        var item = Assert.IsType<TrackQueueItem>(_sut.Items[0]);
        Assert.Equal(moved, item.Track.FileInfo.FullName);
        // Its place and its identity, because the DJ's request did not change: only where the
        // file is did.
        Assert.Equal(queued.Id, item.Id);
        Assert.Equal(2, _sut.Count);
    }

    [Fact]
    public void TheAutoTrackWhoseFileMoved_IsRepointedToo()
    {
        // The preview of what plays next when nobody asks for anything, and it plays like any
        // other track.
        var track = TestData.CreateTrack();
        _sut.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(track, true)));

        var moved = TidiedInto(track, "Naragonia");
        _moved.OnNext(new PathMove(track.FileInfo.FullName, moved));

        var item = Assert.IsType<AutoTrackQueueItem>(_sut.Items[0]);
        Assert.Equal(moved, item.TrackQueueItem.Track.FileInfo.FullName);
    }

    /// <summary>Where a track's file ends up when its folder is tidied up under it.</summary>
    /// <remarks>
    /// Built from the path the fixture's own filesystem produced, because a path spelled out here
    /// is a different string on Windows and the assertion compares them.
    /// </remarks>
    private static string TidiedInto(Track track, string folder)
    {
        var fileSystem = track.FileInfo.FileSystem;
        return fileSystem.Path.Combine(
            fileSystem.Path.GetDirectoryName(track.FileInfo.FullName)!, folder, track.FileInfo.Name);
    }

    // --- Basic ops ---

    [Fact]
    public void Enqueue_AddsItem()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        Assert.True(_sut.Enqueue(track).Allowed);
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void Dequeue_ReturnsFirstItem()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _sut.Enqueue(track);
        var result = _sut.Dequeue();
        Assert.Equal(track, result);
        Assert.Equal(0, _sut.Count);
    }

    [Fact]
    public void Dequeue_Empty_ReturnsNull() => Assert.Null(_sut.Dequeue());

    [Fact]
    public void Peek_ReturnsFirstWithoutRemoving()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _sut.Enqueue(track);
        Assert.Equal(track, _sut.Peek());
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void Peek_Empty_ReturnsNull() => Assert.Null(_sut.Peek());

    [Fact]
    public void InsertAt_InsertsAtPosition()
    {
        var track1 = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var track2 = new TrackQueueItem(TestData.CreateTrack("B"), false);
        var track3 = new TrackQueueItem(TestData.CreateTrack("C"), false);
        _sut.Enqueue(track1);
        _sut.Enqueue(track2);
        _sut.InsertAt(1, track3);
        Assert.Equal(track3, _sut.Items[1]);
    }

    [Fact]
    public void Move_ChangesPosition()
    {
        var track1 = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var track2 = new TrackQueueItem(TestData.CreateTrack("B"), false);
        _sut.Enqueue(track1);
        _sut.Enqueue(track2);
        Assert.Equal(QueueChangeResult.Done, _sut.Move(track1.Id, 1));
        Assert.Equal(track2, _sut.Items[0]);
        Assert.Equal(track1, _sut.Items[1]);
    }

    [Fact]
    public void Remove_RemovesItem()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _sut.Enqueue(track);
        Assert.Equal(QueueChangeResult.Done, _sut.Remove(track.Id));
        Assert.Equal(0, _sut.Count);
    }

    // --- A queue that shifted under the request ---

    [Fact]
    public void Remove_TheRowPlayedFirst_TakesNothingElseWithIt()
    {
        // The DJ presses Delete on the second row in the same moment the first one ends. Read as a
        // position, "row one" is by then the row below the one they were looking at.
        var playing = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var wanted = new TrackQueueItem(TestData.CreateTrack("B"), false);
        var innocent = new TrackQueueItem(TestData.CreateTrack("C"), false);
        _sut.Enqueue(playing);
        _sut.Enqueue(wanted);
        _sut.Enqueue(innocent);

        _sut.Dequeue();

        Assert.Equal(QueueChangeResult.Done, _sut.Remove(wanted.Id));
        Assert.Same(innocent, Assert.Single(_sut.Items));
    }

    [Fact]
    public void Remove_TheRowItselfPlayed_SaysTheQueueMovedOn()
    {
        var played = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var rest = new TrackQueueItem(TestData.CreateTrack("B"), false);
        _sut.Enqueue(played);
        _sut.Enqueue(rest);

        _sut.Dequeue();

        // Not a refusal: the queue has nothing against this row, it has moved past it, and the
        // caller's list is the thing that is out of date.
        Assert.Equal(QueueChangeResult.Gone, _sut.Remove(played.Id));
        Assert.Same(rest, Assert.Single(_sut.Items));
    }

    [Fact]
    public void Move_TheRowPlayedFirst_MovesTheRowThatWasAskedFor()
    {
        var playing = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var wanted = new TrackQueueItem(TestData.CreateTrack("B"), false);
        var innocent = new TrackQueueItem(TestData.CreateTrack("C"), false);
        _sut.Enqueue(playing);
        _sut.Enqueue(wanted);
        _sut.Enqueue(innocent);

        _sut.Dequeue();

        Assert.Equal(QueueChangeResult.Done, _sut.Move(wanted.Id, 1));
        Assert.Same(innocent, _sut.Items[0]);
        Assert.Same(wanted, _sut.Items[1]);
    }

    [Fact]
    public void Move_TheRowItselfPlayed_SaysTheQueueMovedOn()
    {
        var played = new TrackQueueItem(TestData.CreateTrack("A"), false);
        _sut.Enqueue(played);
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));

        _sut.Dequeue();

        Assert.Equal(QueueChangeResult.Gone, _sut.Move(played.Id, 0));
    }

    [Fact]
    public void Move_PastTheEndOfTheQueue_IsRefusedRatherThanThrowing()
    {
        // What the phone's "down" button asks for when its list still has a row below this one.
        var last = new TrackQueueItem(TestData.CreateTrack("B"), false);
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(last);

        _sut.Dequeue();

        Assert.Equal(QueueChangeResult.Refused, _sut.Move(last.Id, 1));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));
        Assert.True(_sut.Clear());
        Assert.Equal(0, _sut.Count);
    }

    [Fact]
    public void Items_ReturnsAllItems()
    {
        var track1 = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var track2 = new TrackQueueItem(TestData.CreateTrack("B"), false);
        _sut.Enqueue(track1);
        _sut.Enqueue(track2);
        Assert.Equal(2, _sut.Items.Count);
    }

    // --- AutoTrack rules ---

    [Fact]
    public void Enqueue_AutoTrack_EmptyQueue_Succeeds()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        Assert.True(_sut.Enqueue(auto).Allowed);
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void Enqueue_AutoTrack_NonEmptyQueue_AppendsAtTail()
    {
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        Assert.True(_sut.Enqueue(auto).Allowed);
        Assert.Equal(2, _sut.Count);
        Assert.IsType<AutoTrackQueueItem>(_sut.Items[^1]);
    }

    [Fact]
    public void Enqueue_AutoTrack_SecondOne_Fails()
    {
        _sut.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("A"), true)));
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("B"), true));
        Assert.False(_sut.Enqueue(auto).Allowed);
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void Enqueue_Regular_InsertsAboveAutoTrack()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        var track = new TrackQueueItem(TestData.CreateTrack("B"), false);
        _sut.Enqueue(track);
        Assert.Equal(2, _sut.Count);
        Assert.IsType<TrackQueueItem>(_sut.Peek());
        Assert.IsType<AutoTrackQueueItem>(_sut.Items[^1]);
    }

    [Fact]
    public void InsertAt_AutoTrack_EmptyQueue_Succeeds()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        Assert.True(_sut.InsertAt(0, auto).Allowed);
    }

    [Fact]
    public void InsertAt_AutoTrack_NonEmptyQueue_GoesToTail()
    {
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        Assert.True(_sut.InsertAt(0, auto).Allowed);
        Assert.IsType<AutoTrackQueueItem>(_sut.Items[^1]);
    }

    [Fact]
    public void InsertAt_Regular_ClampedAboveAutoTrack()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        var track = new TrackQueueItem(TestData.CreateTrack("B"), false);
        _sut.InsertAt(1, track);
        Assert.Equal(2, _sut.Count);
        Assert.IsType<TrackQueueItem>(_sut.Peek());
        Assert.IsType<AutoTrackQueueItem>(_sut.Items[^1]);
    }

    [Fact]
    public void Move_Regular_CannotGoBelowAutoTrack()
    {
        var first = new TrackQueueItem(TestData.CreateTrack("A"), false);
        _sut.Enqueue(first);
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));
        _sut.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Auto"), true)));

        Assert.Equal(QueueChangeResult.Done, _sut.Move(first.Id, 2));

        Assert.IsType<AutoTrackQueueItem>(_sut.Items[^1]);
        Assert.Equal("A", ((TrackQueueItem)_sut.Items[1]).Track.Dance);
    }

    [Fact]
    public void Clear_KeepsAutoTrack()
    {
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Auto"), true)));

        Assert.True(_sut.Clear());

        Assert.Equal(1, _sut.Count);
        Assert.IsType<AutoTrackQueueItem>(_sut.Peek());
    }

    [Fact]
    public void Move_AutoTrack_IsRefused()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        Assert.Equal(QueueChangeResult.Refused, _sut.Move(auto.Id, 0));
    }

    [Fact]
    public void Remove_AutoTrack_IsRefused()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        Assert.Equal(QueueChangeResult.Refused, _sut.Remove(auto.Id));
    }

    [Fact]
    public void Clear_OnlyAutoTrack_ReturnsFalse()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        Assert.False(_sut.Clear());
    }

    [Fact]
    public void RemoveWhere_RemovesMatchingItems()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        Assert.True(_sut.RemoveWhere(i => i is AutoTrackQueueItem));
        Assert.Equal(0, _sut.Count);
    }

    [Fact]
    public void RemoveWhere_NoMatch_ReturnsFalse() =>
        Assert.False(_sut.RemoveWhere(i => i is AutoTrackQueueItem));

    // --- Max items ---

    [Fact]
    public void Enqueue_QueueFull_ReturnsDenied()
    {
        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 2
        });

        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));

        var result = _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("C"), false));
        Assert.False(result.Allowed);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, DomainStrings.MaxItemsRule_QueueFull, 2), result.RejectionReason!);
    }

    [Fact]
    public void InsertAt_QueueFull_ReturnsDenied()
    {
        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 2
        });

        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));

        var result = _sut.InsertAt(0, new TrackQueueItem(TestData.CreateTrack("C"), false));
        Assert.False(result.Allowed);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, DomainStrings.MaxItemsRule_QueueFull, 2), result.RejectionReason!);
    }

    // --- Reactive ---

    [Fact]
    public void Connect_EmitsChangeSets()
    {
        var changeSetCount = 0;
        using var sub = _sut.Connect().Subscribe(_ => changeSetCount++);

        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _sut.Enqueue(track);
        Assert.True(changeSetCount >= 1);

        var before = changeSetCount;
        _sut.Remove(track.Id);
        Assert.True(changeSetCount > before);
    }

    // --- Eviction ---

    [Fact]
    public void SettingsChange_MaxReduced_EvictsExcess()
    {
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("C"), false));
        Assert.Equal(3, _sut.Count);

        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 2
        });
        Assert.Equal(2, _sut.Count);
    }

    [Fact]
    public void SettingsChange_DuplicatesDisabled_EvictsDuplicates()
    {
        // Start with duplicates allowed
        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 100,
            AllowDuplicateTracksInQueue = true
        });

        var track = TestData.CreateTrack();
        _sut.Enqueue(new TrackQueueItem(track, false));
        _sut.Enqueue(new TrackQueueItem(track, false));
        Assert.Equal(2, _sut.Count);

        // Disable duplicates
        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 100,
            AllowDuplicateTracksInQueue = false
        });
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void HistoryChange_EvictsDuplicates()
    {
        // Disable duplicates from the start
        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 100,
            AllowDuplicateTracksInQueue = false
        });

        var track = TestData.CreateTrack();
        _sut.Enqueue(new TrackQueueItem(track, false));
        Assert.Equal(1, _sut.Count);

        // Simulate track finishing: appears in history
        _historySubject.OnNext(new QueueHistory(null,
        [
            new TrackHistoryEntry(track.FileInfo.FullName, "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]));

        Assert.Equal(0, _sut.Count);
    }

    // The guard is handed a snapshot and answers in positions counted against it, and working out
    // the answer means asking what is playing. That question goes to the thing that takes finished
    // dances off the queue, so the queue can move between the count and the removal. These two put
    // the move in that window on purpose, which is the only way to make the race arrive every time.

    [Fact]
    public void Evict_TheQueueShrankWhileTheGuardWasThinking_TakesOutTheRowsItNamed()
    {
        var a = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var b = new TrackQueueItem(TestData.CreateTrack("B"), false);
        var c = new TrackQueueItem(TestData.CreateTrack("C"), false);
        var d = new TrackQueueItem(TestData.CreateTrack("D"), false);
        _sut.Enqueue(a);
        _sut.Enqueue(b);
        _sut.Enqueue(c);
        _sut.Enqueue(d);

        var shifted = false;
        _currentItem = () =>
        {
            if (!shifted)
            {
                shifted = true;
                _sut.Dequeue();
            }

            return null;
        };

        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 3
        });

        // The row over the limit is D, and D is what goes. As a position it was row three, which is
        // off the end of the three rows left once A had played.
        Assert.Equal<IQueueItem>([b, c], _sut.Items);
    }

    [Fact]
    public void Evict_TheQueueWasReorderedWhileTheGuardWasThinking_LeavesTheInnocentRowAlone()
    {
        var a = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var b = new TrackQueueItem(TestData.CreateTrack("B"), false);
        var c = new TrackQueueItem(TestData.CreateTrack("C"), false);
        var d = new TrackQueueItem(TestData.CreateTrack("D"), false);
        var e = new TrackQueueItem(TestData.CreateTrack("E"), false);
        _sut.Enqueue(a);
        _sut.Enqueue(b);
        _sut.Enqueue(c);
        _sut.Enqueue(d);
        _sut.Enqueue(e);

        var moved = false;
        _currentItem = () =>
        {
            if (!moved)
            {
                moved = true;
                _sut.Move(d.Id, 0);
            }

            return null;
        };

        _settingsSubject.OnNext(new ApplicationSettings() with
        {
            MaxQueueItems = 3
        });

        // D and E are the rows over the limit. Read as positions three and four, the drag would
        // have cost C its place in the evening and left D sitting at the top.
        Assert.Equal<IQueueItem>([a, b, c], _sut.Items);
    }

    // --- End of the night ---

    private static EndOfNightQueueItem EndOfNight() => new("/audio/last-waltz.mp3", TimeSpan.FromMinutes(4));

    [Fact]
    public void Enqueue_EndOfNight_TakesTheAutoTrackWithItAndGoesLast()
    {
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("B"), true)));

        Assert.True(_sut.Enqueue(EndOfNight()).Allowed);

        Assert.Equal(2, _sut.Count);
        Assert.DoesNotContain(_sut.Items, i => i is AutoTrackQueueItem);
        Assert.IsType<EndOfNightQueueItem>(_sut.Items[^1]);
    }

    [Fact]
    public void Enqueue_AfterEndOfNight_IsRefused()
    {
        _sut.Enqueue(EndOfNight());

        var result = _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));

        Assert.False(result.Allowed);
        Assert.Equal(QueueDenial.EveningEnded, result.Denial);
        Assert.Equal(1, _sut.Count);
    }

    [Fact]
    public void Remove_EndOfNight_ReopensTheEvening()
    {
        var endOfNight = EndOfNight();
        _sut.Enqueue(endOfNight);

        Assert.Equal(QueueChangeResult.Done, _sut.Remove(endOfNight.Id));
        Assert.True(_sut.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false)).Allowed);
    }

    [Fact]
    public void Move_CannotPushARequestBelowTheEndOfNight()
    {
        var request = new TrackQueueItem(TestData.CreateTrack("A"), false);
        _sut.Enqueue(request);
        _sut.Enqueue(EndOfNight());

        Assert.Equal(QueueChangeResult.Done, _sut.Move(request.Id, 1));

        Assert.IsType<EndOfNightQueueItem>(_sut.Items[^1]);
    }

    [Fact]
    public void Move_EndOfNightItself_IsRefused()
    {
        var endOfNight = EndOfNight();
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(endOfNight);

        Assert.Equal(QueueChangeResult.Refused, _sut.Move(endOfNight.Id, 0));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _settingsSubject.Dispose();
        _historySubject.Dispose();
    }
}
