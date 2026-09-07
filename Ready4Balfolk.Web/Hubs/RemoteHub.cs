using Microsoft.AspNetCore.SignalR;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Web.Contracts;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Web.Hubs;

/// <summary>The phone remote's connection: everything that can change something.</summary>
/// <remarks>
/// Every method here reaches the queue or the audio engine, both of which are driven from the UI
/// thread, so every method goes through <see cref="IRemoteCommandDispatcher"/> rather than running
/// on the threadpool thread SignalR handed it.
/// </remarks>
public sealed class RemoteHub(
    PresentationBroadcaster broadcaster,
    RemoteAccessService access,
    IRemoteCommandDispatcher dispatcher,
    IQueueService queueService,
    IQueueConsumptionService consumptionService,
    IEndOfNightAudio endOfNightAudio,
    IRandomTrackService randomTrackService,
    IDancePool dancePool,
    ITrackStore trackStore,
    ISettingsStore settingsStore) : Hub
{
    /// <summary>The client method the queue list is pushed to.</summary>
    public const string QueueMethod = "queue";

    /// <summary>The client method that says this phone is not let in any more.</summary>
    /// <remarks>
    /// Sent before the connection goes, and again by the filter on any command from a token that
    /// has stopped being good. A remote that silently does nothing reads as a crashed application,
    /// and the honest answer is the PIN form: the remote is there, the helper needs the new PIN.
    /// </remarks>
    public const string TurnedOutMethod = "turnedOut";

    /// <summary>How many search results a phone screen is sent.</summary>
    private const int MaxSearchResults = 40;

    /// <summary>The longest a message can be, matching the desktop dialog's own <c>MaxLength</c>.</summary>
    /// <remarks>
    /// The textarea on the phone page is a courtesy, not a guard: anybody on the venue wifi who has
    /// the PIN can call this method directly with whatever a browser's dev tools, or nothing running
    /// a browser at all, will send. This is the one place the limit actually holds.
    /// </remarks>
    private const int MaxMessageLength = 60;

    /// <summary>Rejects the connection outright unless it carries a token issued for the PIN.</summary>
    public override async Task OnConnectedAsync()
    {
        if (!access.IsTokenValid(RemoteTokenFilter.TokenOf(Context)))
        {
            await Clients.Caller.SendAsync(TurnedOutMethod);
            Context.Abort();
            return;
        }

        await Clients.Caller.SendAsync(DisplayHub.SnapshotMethod, broadcaster.Latest);
        await Clients.Caller.SendAsync(QueueMethod, broadcaster.QueueSnapshot);
        await base.OnConnectedAsync();
    }

    /// <summary>Holds what is on, and starts the evening when nothing is on at all.</summary>
    /// <remarks>
    /// The phone's buttons are drawn from a snapshot up to half a second old, so a tap can be about
    /// a dance that has ended: during the moment between two dances there is nothing loaded to hold,
    /// and after the last one there is nothing at all. Refused rather than acted on, and the answer
    /// says the phone's screen has been passed, which the page words itself and redraws for.
    /// </remarks>
    public async Task<CommandResultDto> PlayPause() =>
        await OnTheUiThread(consumptionService.PlayPauseAsync) ? CommandResultDto.Ok : CommandResultDto.Stale;

    /// <inheritdoc cref="PlayPause" />
    public async Task<CommandResultDto> Restart() =>
        await OnTheUiThread(consumptionService.RestartAsync) ? CommandResultDto.Ok : CommandResultDto.Stale;

    /// <summary>Runs work that hands back a task, and waits for the work rather than its start.</summary>
    /// <remarks>
    /// The generic dispatcher overload only waits for the call that makes the task, so without the
    /// unwrap the phone would be told the answer before the command had run.
    /// </remarks>
    private Task<bool> OnTheUiThread(Func<Task<bool>> work) => dispatcher.InvokeAsync(work).Unwrap();

    /// <summary>Skips the current item. The page holds a button down to get here.</summary>
    /// <remarks>
    /// The result is swallowed inside the lambda rather than returned from it: handed back, the
    /// Func&lt;T&gt; overload wins over Func&lt;Task&gt; and the skip is no longer waited for.
    /// </remarks>
    public Task Skip() => dispatcher.InvokeAsync(async () => { await consumptionService.AdvanceAsync(); });

    public Task<CommandResultDto> QueueRandom() => dispatcher.InvokeAsync(() =>
    {
        // The same pool the panel is showing, so the phone and the screen never disagree about
        // what is being drawn from.
        var track = randomTrackService.PickRandomTrack(
            dancePool.Scope,
            settingsStore.Current.AllowDuplicateTracksInQueue);

        return track is null
            ? new CommandResultDto(false, "No track could be picked from the dance list")
            : Enqueue(new TrackQueueItem(track, true));
    });

    public Task<CommandResultDto> QueueDelay(int seconds) => dispatcher.InvokeAsync(() =>
        Enqueue(new DelayQueueItem(TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 900)))));

    public Task<CommandResultDto> QueueStop() => dispatcher.InvokeAsync(() =>
        Enqueue(new StopQueueItem()));

    /// <summary>
    /// Ends the night from the phone, which is where somebody stacking chairs is standing.
    /// </summary>
    /// <remarks>
    /// The file lives in the settings on the computer, so there is nothing for the phone to choose:
    /// it can only say that it is time. With none named, saying so is the whole answer.
    /// </remarks>
    public Task<CommandResultDto> QueueEndOfNight() => dispatcher.InvokeAsync(() =>
        endOfNightAudio.Create() is { } item
            ? Enqueue(item)
            : new CommandResultDto(false, "No end-of-the-night audio has been chosen at the computer"));

    public Task<CommandResultDto> QueueMessage(string text) => dispatcher.InvokeAsync(() =>
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new CommandResultDto(false, "A message needs some text");
        }

        var trimmed = text.Trim();
        return trimmed.Length > MaxMessageLength
            ? new CommandResultDto(false, $"A message can be at most {MaxMessageLength} characters")
            : Enqueue(new MessageQueueItem(trimmed));
    });

    public Task<CommandResultDto> QueueTrack(string id) => dispatcher.InvokeAsync(() =>
    {
        var track = FindTrack(id);
        return track is null
            ? new CommandResultDto(false, "That track is no longer in the library")
            : Enqueue(new TrackQueueItem(track, false));
    });

    /// <summary>
    /// Rearranging by the name of the row, never by the number it was drawn at.
    /// </summary>
    /// <remarks>
    /// The list a phone is holding is up to half a second old and every dance that ends renumbers
    /// it, so "row three" arriving here means whatever row three has become. A tap that arrives
    /// after its row has gone is the ordinary case rather than an error, and it comes back as a
    /// queue that has moved on, which the page can say and redraw for: it used to come back as a
    /// failed invoke, and the page called a healthy connection lost.
    /// </remarks>
    public Task<CommandResultDto> MoveUp(string id) => Rearrange(id, -1, "That item cannot move up");

    public Task<CommandResultDto> MoveDown(string id) => Rearrange(id, 1, "That item cannot move down");

    private Task<CommandResultDto> Rearrange(string id, int offset, string refusal) =>
        dispatcher.InvokeAsync(() => QueueItemId.TryParse(id, out var itemId)
            ? MoveNeighbour(itemId, offset, refusal)
            : CommandResultDto.Stale);

    private CommandResultDto MoveNeighbour(QueueItemId id, int offset, string refusal)
    {
        // Where the row is now is read here rather than sent from the phone. This runs where the
        // queue is driven from, so nothing can move between finding the row and moving it.
        var index = queueService.IndexOf(id);

        return index < 0
            ? CommandResultDto.Stale
            : Answer(queueService.Move(id, index + offset), refusal);
    }

    public Task<CommandResultDto> Remove(string id) => dispatcher.InvokeAsync(() =>
        QueueItemId.TryParse(id, out var itemId)
            ? Answer(queueService.Remove(itemId), "That item cannot be removed")
            : CommandResultDto.Stale);

    private static CommandResultDto Answer(QueueChangeResult result, string refusal) => result switch
    {
        QueueChangeResult.Done => CommandResultDto.Ok,
        QueueChangeResult.Gone => CommandResultDto.Stale,
        _ => new CommandResultDto(false, refusal)
    };

    /// <summary>
    /// Search rather than a full listing: the catalog is a sortable grid on a desk, and a phone gets
    /// the one thing that actually finds a track.
    /// </summary>
    public Task<IReadOnlyList<TrackHitDto>> Search(string? term) => dispatcher.InvokeAsync(() =>
    {
        var needle = term?.Trim() ?? string.Empty;

        var matches = trackStore.Current
            .Where(track => needle.Length == 0 || Matches(track, needle))
            .Take(MaxSearchResults)
            .Select(track => new TrackHitDto(
                track.FileInfo.FullName,
                track.Dance,
                track.Artist,
                track.Title,
                track.Length.TotalSeconds))
            .ToList();

        return (IReadOnlyList<TrackHitDto>)matches;
    });

    private static bool Matches(Track track, string needle) =>
        track.Dance.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || track.Artist.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || track.Title.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private Track? FindTrack(string id) => trackStore.Current
        .FirstOrDefault(track => string.Equals(track.FileInfo.FullName, id, StringComparison.Ordinal));

    /// <summary>Enqueues, and hands the queue guard's own refusal straight back to the phone.</summary>
    private CommandResultDto Enqueue(IQueueItem item)
    {
        var result = queueService.Enqueue(item);
        return result.Allowed ? CommandResultDto.Ok : new CommandResultDto(false, result.RejectionReason);
    }
}
