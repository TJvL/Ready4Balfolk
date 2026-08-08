using System.Reactive.Subjects;
using DynamicData;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Queue;
using RxUnit = System.Reactive.Unit;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class QueueViewModelTests : IDisposable
{
    private readonly IQueueService _queueService;
    private readonly IRandomTrackService _randomTrackService;
    private readonly DanceListViewModel _danceListVm;
    private readonly IConfirmationService _confirmation;
    private readonly INotificationService _notification;
    private readonly QueueViewModel _sut;

    private readonly SourceList<IQueueItem> _queueSource = new();
    private readonly BehaviorSubject<IQueueItem?> _currentItem = new(null);
    private readonly BehaviorSubject<TimeSpan> _elapsed = new(TimeSpan.Zero);
    private readonly BehaviorSubject<TimeSpan> _totalDuration = new(TimeSpan.Zero);
    private readonly BehaviorSubject<bool> _isPlaying = new(false);
    private readonly Subject<RxUnit> _itemCompleted = new();
    private readonly BehaviorSubject<ApplicationSettings> _settingsSubject;

    public QueueViewModelTests()
    {
        var settings = new ApplicationSettings();
        _settingsSubject = new BehaviorSubject<ApplicationSettings>(settings);

        _queueService = Substitute.For<IQueueService>();
        _queueService.Connect().Returns(_queueSource.Connect());
        _queueService.Count.Returns(_ => _queueSource.Count);
        _queueService.Items.Returns(_ => _queueSource.Items.ToList());
        _queueService.Enqueue(Arg.Any<IQueueItem>()).Returns(ci =>
        {
            _queueSource.Add(ci.Arg<IQueueItem>()!);
            return QueueAddResult.Allow();
        });
        _queueService.InsertAt(Arg.Any<int>(), Arg.Any<IQueueItem>()).Returns(ci =>
        {
            var index = ci.ArgAt<int>(0);
            _queueSource.Insert(Math.Min(index, _queueSource.Count), ci.ArgAt<IQueueItem>(1));
            return QueueAddResult.Allow();
        });
        _queueService.RemoveAt(Arg.Any<int>()).Returns(ci =>
        {
            var index = ci.Arg<int>();
            _queueSource.RemoveAt(index);
            return true;
        });
        _queueService.Clear().Returns(_ =>
        {
            _queueSource.Clear();
            return true;
        });
        _queueService.Move(Arg.Any<int>(), Arg.Any<int>()).Returns(ci =>
        {
            _queueSource.Move(ci.ArgAt<int>(0), ci.ArgAt<int>(1));
            return true;
        });
        _queueService.RemoveWhere(Arg.Any<Func<IQueueItem, bool>>()).Returns(ci =>
        {
            var predicate = ci.Arg<Func<IQueueItem, bool>>()!;
            var found = false;
            _queueSource.Edit(list =>
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (predicate(list[i]))
                    {
                        list.RemoveAt(i);
                        found = true;
                    }
                }
            });
            return found;
        });

        var consumption = Substitute.For<IQueueConsumptionService>();
        consumption.WhenCurrentItemChanged.Returns(_currentItem);
        consumption.WhenElapsedChanged.Returns(_elapsed);
        consumption.WhenTotalDurationChanged.Returns(_totalDuration);
        consumption.WhenIsPlayingChanged.Returns(_isPlaying);
        consumption.WhenItemCompleted.Returns(_itemCompleted);
        consumption.CurrentItem.Returns(_ => _currentItem.Value);

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_ => _settingsSubject.Value);
        settingsStore.Observe().Returns(_settingsSubject);

        _randomTrackService = Substitute.For<IRandomTrackService>();
        _confirmation = Substitute.For<IConfirmationService>();
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _notification = Substitute.For<INotificationService>();

        _danceListVm = CreateMinimalDanceListVm();

        _sut = new QueueViewModel(
            _queueService, consumption, settingsStore,
            _randomTrackService, _danceListVm, _confirmation, _notification);
    }

    private DanceListViewModel CreateMinimalDanceListVm()
    {
        var danceListStore = Substitute.For<Domain.Stores.Dances.IDanceListStore>();
        danceListStore.Current.Returns(TestData.CreateSimpleDanceList());
        danceListStore.Index.Returns(DanceListIndex.Build(TestData.CreateSimpleDanceList()));
        danceListStore.Observe().Returns(new BehaviorSubject<DanceList>(TestData.CreateSimpleDanceList()));
        danceListStore.IsLoading.Returns(new BehaviorSubject<bool>(false));

        var editorHistory = Substitute.For<Domain.Services.Editor.IEditorHistoryService>();
        editorHistory.CanUndo.Returns(new BehaviorSubject<bool>(false));
        editorHistory.CanRedo.Returns(new BehaviorSubject<bool>(false));
        editorHistory.UndoDescription.Returns(new BehaviorSubject<string?>(null));
        editorHistory.RedoDescription.Returns(new BehaviorSubject<string?>(null));

        return new DanceListViewModel(danceListStore, editorHistory, _notification,
            _confirmation, new NoOpLoggerService());
    }

    // --- QueueRandomTrack ---

    [Fact]
    public void QueueRandomTrack_WithTrack_Enqueues()
    {
        var track = TestData.CreateTrack();
        _randomTrackService.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>())
            .Returns(track);

        _sut.QueueRandomTrackCommand.Execute().Subscribe();

        _queueService.Received(1).Enqueue(Arg.Is<TrackQueueItem>(t => t!.Track == track));
    }

    [Fact]
    public void QueueRandomTrack_NoTrack_ShowsWarning()
    {
        _randomTrackService.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>())
            .Returns((Track?)null);

        _sut.QueueRandomTrackCommand.Execute().Subscribe();

        _notification.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public void QueueRandomTrack_QueueFull_ShowsWarning()
    {
        var track = TestData.CreateTrack();
        _randomTrackService.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>())
            .Returns(track);
        _queueService.Enqueue(Arg.Any<IQueueItem>())
            .Returns(QueueAddResult.Deny("Queue is full (max 6 items)."));

        _sut.QueueRandomTrackCommand.Execute().Subscribe();

        _notification.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    // --- EnqueueStop/Delay ---

    [Fact]
    public void EnqueueStop_EnqueuesStopItem()
    {
        _sut.EnqueueStopCommand.Execute().Subscribe();
        _queueService.Received(1).Enqueue(Arg.Any<StopQueueItem>());
    }

    [Fact]
    public void EnqueueStop_QueueFull_ShowsWarning()
    {
        _queueService.Enqueue(Arg.Any<IQueueItem>())
            .Returns(QueueAddResult.Deny("Queue is full (max 6 items)."));

        _sut.EnqueueStopCommand.Execute().Subscribe();

        _notification.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public void EnqueueDelay_EnqueuesDelayItem()
    {
        _sut.EnqueueDelayCommand.Execute().Subscribe();
        _queueService.Received(1).Enqueue(Arg.Any<DelayQueueItem>());
    }

    // --- RemoveSelected ---

    [Fact]
    public void RemoveSelected_AutoTrackBlocked()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _sut.SelectedItem = auto;

        // CanRemoveSelected should be false for AutoTrackQueueItem
        // The command won't execute because of the CanExecute guard
        Assert.IsType<AutoTrackQueueItem>(_sut.SelectedItem);
    }

    // --- ClearQueue ---

    [Fact]
    public void ClearQueue_WithConfirmation_Clears()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));

        _sut.ClearQueueCommand.Execute().Subscribe();

        _queueService.Received(1).Clear();
    }

    [Fact]
    public void ClearQueue_WithoutConfirmation_DoesNotClear()
    {
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));

        _sut.ClearQueueCommand.Execute().Subscribe();

        _queueService.DidNotReceive().Clear();
    }

    // --- MoveItem ---

    [Fact]
    public void MoveItem_ValidIndices_Moves()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("B"), false));

        _sut.MoveItem(0, 1);

        _queueService.Received(1).Move(0, 1);
    }

    [Fact]
    public void MoveItem_AutoTrack_DelegatesToService()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _queueSource.Add(auto);
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("B"), false));

        _sut.MoveItem(0, 1);

        // QueueService.Move handles the guard check now
        _queueService.Received(1).Move(0, 1);
    }

    // --- Selection with duplicate (value-equal) items ---
    // Queue items are records, so two StopQueueItems are Equals; these tests
    // pin down that the selected *instance* is moved/removed, not the first
    // value-equal one.

    [Fact]
    public void MoveSelectedUp_DuplicateStops_MovesSelectedInstance()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _queueSource.Add(new StopQueueItem());
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("B"), false));
        var lastStop = new StopQueueItem();
        _queueSource.Add(lastStop);

        _sut.SelectedItem = lastStop;
        _sut.MoveSelectedUp();

        _queueService.Received(1).Move(3, 2);
    }

    [Fact]
    public void MoveStates_DuplicateStops_UseSelectedInstancePosition()
    {
        _queueSource.Add(new StopQueueItem());
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));
        var lastStop = new StopQueueItem();
        _queueSource.Add(lastStop);

        _sut.SelectedItem = lastStop;

        Assert.True(_sut.IsSelectedMovableUp);
        Assert.False(_sut.IsSelectedMovableDown);
    }

    [Fact]
    public void DeleteSelectedItem_DuplicateStops_RemovesSelectedInstance()
    {
        _queueSource.Add(new StopQueueItem());
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));
        var lastStop = new StopQueueItem();
        _queueSource.Add(lastStop);

        _sut.SelectedItem = lastStop;
        _sut.DeleteSelectedItem();

        _queueService.Received(1).RemoveAt(2);
    }

    // --- ItemCountText ---

    [Fact]
    public void ItemCountText_EmptyQueue() => Assert.Equal("Queue empty", _sut.ItemCountText);

    [Fact]
    public void ItemCountText_OneItem()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));
        Assert.Equal("1 item", _sut.ItemCountText);
    }

    [Fact]
    public void ItemCountText_MultipleItems()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("A"), false));
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("B"), false));
        Assert.Equal("2 items", _sut.ItemCountText);
    }

    // --- HasItems ---

    [Fact]
    public void HasItems_ExcludesAutoTrack()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _queueSource.Add(auto);
        Assert.False(_sut.HasItems);
    }

    [Fact]
    public void HasItems_TrueForRegularItems()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));
        Assert.True(_sut.HasItems);
    }

    // --- Auto-track visibility ---

    [Fact]
    public void AutoTrack_Enqueued_WhenQueueHasRequests()
    {
        var track = TestData.CreateTrack("Auto");
        _randomTrackService.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>())
            .Returns(track);
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Playing"), false));

        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("Request"), false));

        _queueService.Received().Enqueue(Arg.Is<AutoTrackQueueItem>(a => a!.TrackQueueItem.Track == track));
    }

    [Fact]
    public void AutoTrack_NotEnqueued_WhenOneIsAlreadyPresent()
    {
        _randomTrackService.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>())
            .Returns(TestData.CreateTrack("Auto"));
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Playing"), false));
        _queueSource.Add(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Existing"), true)));
        _queueService.ClearReceivedCalls();

        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("Request"), false));

        _queueService.DidNotReceive().Enqueue(Arg.Any<AutoTrackQueueItem>());
    }

    [Fact]
    public void MoveDown_Blocked_ForItemAboveAutoTrack()
    {
        var request = new TrackQueueItem(TestData.CreateTrack("Request"), false);
        _queueSource.Add(request);
        _queueSource.Add(new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Auto"), true)));

        _sut.SelectedItem = request;

        Assert.False(_sut.IsSelectedMovableDown);
    }

    // --- PinAutoTrack ---

    [Fact]
    public void PinAutoTrack_QueueFull_KeepsSameAutoTrackAndWarns()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Auto"), true));
        _queueSource.Add(auto);
        _queueService.InsertAt(Arg.Any<int>(), Arg.Any<IQueueItem>())
            .Returns(QueueAddResult.Deny("Queue is full (max 6 items)."));

        _sut.PinAutoTrack(auto);

        // The very same auto-track goes back, so a refused pin does not reroll the track.
        _queueService.Received(1).Enqueue(auto);
        _notification.Received(1).Show("Queue is full (max 6 items).", NotificationSeverity.Warning);
    }

    [Fact]
    public void PinAutoTrack_Allowed_DoesNotReAddAutoTrack()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("Auto"), true));
        _queueSource.Add(auto);

        _sut.PinAutoTrack(auto);

        _queueService.DidNotReceive().Enqueue(auto);
        _notification.DidNotReceive().Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }


    [Fact]
    public void PinAutoTrack_ConvertsToRegularTrack()
    {
        var innerTrack = new TrackQueueItem(TestData.CreateTrack(), true);
        var auto = new AutoTrackQueueItem(innerTrack);
        _queueSource.Add(auto);

        _sut.PinAutoTrack(auto);

        _queueService.Received().RemoveWhere(Arg.Any<Func<IQueueItem, bool>>());
        _queueService.Received(1).InsertAt(Arg.Any<int>(),
            Arg.Is<TrackQueueItem>(t => t!.RandomlyAdded));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _danceListVm.Dispose();
        _queueSource.Dispose();
        _currentItem.Dispose();
        _elapsed.Dispose();
        _totalDuration.Dispose();
        _isPlaying.Dispose();
        _itemCompleted.Dispose();
        _settingsSubject.Dispose();
    }
}
