using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Which rules the settings put in front of the queue.
/// </summary>
/// <remarks>
/// The rules themselves are covered one at a time elsewhere. What is worth pinning down here is
/// that a switch in the settings panel reaches the queue at all: a rule left out when it should be
/// in is a guard that has quietly stopped guarding, and from the outside that reads exactly like a
/// guard that agreed with you.
/// </remarks>
public sealed class QueueGuardBuilderTests
{
    private readonly IQueueHistoryStore _history = Substitute.For<IQueueHistoryStore>();

    private IQueueItem? _playing;

    public QueueGuardBuilderTests()
    {
        _history.Current.Returns(new QueueHistory(null, []));
    }

    private static ApplicationSettings Defaults => new();

    private IQueueGuard Build(ApplicationSettings settings) =>
        QueueGuardBuilder.FromSettings(settings, () => _playing, _history, () => TimeSpan.Zero, TimeProvider.System);

    private static TrackQueueItem Queued(string title = "Title") =>
        new(TestData.CreateTrack(title: title), RandomlyAdded: false);

    // --- The duplicate switch ---

    [Fact]
    public void DuplicatesAllowed_TheSameTrackTwice_IsLetThrough()
    {
        var guard = Build(Defaults with { AllowDuplicateTracksInQueue = true });
        var already = Queued();

        Assert.True(guard.EvaluateAdd(Queued(), [already]).Allowed);
    }

    [Fact]
    public void DuplicatesRefused_TheSameTrackTwice_IsStopped()
    {
        var guard = Build(Defaults with { AllowDuplicateTracksInQueue = false });
        var already = Queued();

        Assert.False(guard.EvaluateAdd(Queued(), [already]).Allowed);
    }

    // --- The cutoff switch ---

    /// <summary>
    /// Midnight with no grace, so the cutoff for the evening in progress has always already gone
    /// by and no clock has to be injected to make the rule bite.
    /// </summary>
    private static ApplicationSettings PastTheCutoff(bool enabled) => Defaults with
    {
        QueueCutoffEnabled = enabled,
        QueueCutoffMinutesOfDay = 0,
        QueueCutoffGraceMinutes = 0
    };

    [Fact]
    public void CutoffOff_AnEntryPastTheCutoffTime_IsLetThrough() =>
        Assert.True(Build(PastTheCutoff(enabled: false)).EvaluateAdd(Queued(), []).Allowed);

    [Fact]
    public void CutoffOn_AnEntryPastTheCutoffTime_IsStopped()
    {
        var result = Build(PastTheCutoff(enabled: true)).EvaluateAdd(Queued(), []);

        Assert.False(result.Allowed);
        Assert.Equal(QueueDenial.Cutoff, result.Denial);
    }

    // --- The rules no switch can take away ---

    [Fact]
    public void MaxItems_IsAlwaysInForce()
    {
        var guard = Build(Defaults with { MaxQueueItems = 1 });

        Assert.False(guard.EvaluateAdd(Queued("Second"), [Queued("First")]).Allowed);
    }

    [Fact]
    public void ASecondAutoTrack_IsRefusedEvenWithTheAutoQueueOn()
    {
        var guard = Build(Defaults with { AutoQueueRandomTrack = true });
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), RandomlyAdded: true));

        Assert.False(guard.EvaluateAdd(auto, [auto]).Allowed);
    }

    [Fact]
    public void TheEveningEnded_ClosesTheQueueBeforeAnyOtherRuleHasAnOpinion()
    {
        // A queue that is both closed and full. The reason a person reads has to be the one that
        // explains what happened, and "the queue is full" would send them to empty it.
        _playing = new EndOfNightQueueItem("/music/last.mp3", TimeSpan.FromMinutes(3));
        var guard = Build(Defaults with { MaxQueueItems = 0 });

        var result = guard.EvaluateAdd(Queued(), []);

        Assert.False(result.Allowed);
        Assert.Equal(QueueDenial.EveningEnded, result.Denial);
    }

    // --- The auto-queue switch, which evicts rather than refuses ---

    [Fact]
    public void AutoQueueOn_AnAutoTrackAlreadyThere_IsLeftAlone()
    {
        IReadOnlyList<IQueueItem> items =
            [new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), RandomlyAdded: true))];

        Assert.Empty(Build(Defaults with { AutoQueueRandomTrack = true }).GetEvictionIndices(items));
    }

    [Fact]
    public void AutoQueueOff_AnAutoTrackAlreadyThere_IsEvicted()
    {
        // Switching the auto-queue off has to clear the one already queued, or the machine keeps
        // the evening going once more after being told to stop.
        IReadOnlyList<IQueueItem> items =
            [new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), RandomlyAdded: true))];

        Assert.Equal([0], Build(Defaults with { AutoQueueRandomTrack = false }).GetEvictionIndices(items));
    }
}
