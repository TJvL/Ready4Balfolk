using System.Globalization;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class QueueServiceTests : IDisposable
{
    private readonly QueueService _sut;
    private readonly BehaviorSubject<ApplicationSettings> _settingsSubject;
    private readonly BehaviorSubject<QueueHistory> _historySubject;

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

        _sut = new QueueService(settingsStore, historyStore, () => null, new NoOpLoggerService());
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
        Assert.True(_sut.Move(0, 1));
        Assert.Equal(track2, _sut.Items[0]);
        Assert.Equal(track1, _sut.Items[1]);
    }

    [Fact]
    public void RemoveAt_RemovesItem()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _sut.Enqueue(track);
        Assert.True(_sut.RemoveAt(0));
        Assert.Equal(0, _sut.Count);
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
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack("B"), false));
        _sut.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Auto"), true)));

        Assert.True(_sut.Move(0, 2));

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
    public void Move_AutoTrack_ReturnsFalse()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        Assert.False(_sut.Move(0, 0));
    }

    [Fact]
    public void RemoveAt_AutoTrack_ReturnsFalse()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.Enqueue(auto);
        Assert.False(_sut.RemoveAt(0));
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

        _sut.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        Assert.True(changeSetCount >= 1);

        var before = changeSetCount;
        _sut.RemoveAt(0);
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

        // Simulate track finishing — appears in history
        _historySubject.OnNext(new QueueHistory(null,
        [
            new TrackHistoryEntry(track.FileInfo.FullName, "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]));

        Assert.Equal(0, _sut.Count);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _settingsSubject.Dispose();
        _historySubject.Dispose();
    }
}
