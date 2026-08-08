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
    IRandomTrackService randomTrackService,
    ITrackStore trackStore,
    ISettingsStore settingsStore) : Hub
{
    /// <summary>The client method the queue list is pushed to.</summary>
    public const string QueueMethod = "queue";

    /// <summary>How many search results a phone screen is sent.</summary>
    private const int MaxSearchResults = 40;

    /// <summary>Rejects the connection outright unless it carries a token issued for the PIN.</summary>
    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
        if (!access.IsTokenValid(token))
        {
            Context.Abort();
            return;
        }

        await Clients.Caller.SendAsync(DisplayHub.SnapshotMethod, broadcaster.Latest);
        await Clients.Caller.SendAsync(QueueMethod, broadcaster.QueueSnapshot);
        await base.OnConnectedAsync();
    }

    public Task PlayPause() => dispatcher.InvokeAsync(consumptionService.PlayPauseAsync);

    public Task Restart() => dispatcher.InvokeAsync(consumptionService.RestartAsync);

    /// <summary>Skips the current item. The page holds a button down to get here.</summary>
    public Task Skip() => dispatcher.InvokeAsync(consumptionService.AdvanceAsync);

    public Task<CommandResultDto> QueueRandom() => dispatcher.InvokeAsync(() =>
    {
        var track = randomTrackService.PickRandomTrack(
            new RandomSelectionScope.EntireList(),
            settingsStore.Current.AllowDuplicateTracksInQueue);

        return track is null
            ? new CommandResultDto(false, "No track could be picked from the dance tree")
            : Enqueue(new TrackQueueItem(track, true));
    });

    public Task<CommandResultDto> QueueDelay(int seconds) => dispatcher.InvokeAsync(() =>
        Enqueue(new DelayQueueItem(TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 900)))));

    public Task<CommandResultDto> QueueStop() => dispatcher.InvokeAsync(() =>
        Enqueue(new StopQueueItem()));

    public Task<CommandResultDto> QueueMessage(string text) => dispatcher.InvokeAsync(() =>
        string.IsNullOrWhiteSpace(text)
            ? new CommandResultDto(false, "A message needs some text")
            : Enqueue(new MessageQueueItem(text.Trim())));

    public Task<CommandResultDto> QueueTrack(string id) => dispatcher.InvokeAsync(() =>
    {
        var track = FindTrack(id);
        return track is null
            ? new CommandResultDto(false, "That track is no longer in the library")
            : Enqueue(new TrackQueueItem(track, false));
    });

    public Task<CommandResultDto> MoveUp(int index) => dispatcher.InvokeAsync(() =>
        index > 0 && queueService.Move(index, index - 1)
            ? CommandResultDto.Ok
            : new CommandResultDto(false, "That item cannot move up"));

    public Task<CommandResultDto> MoveDown(int index) => dispatcher.InvokeAsync(() =>
        queueService.Move(index, index + 1)
            ? CommandResultDto.Ok
            : new CommandResultDto(false, "That item cannot move down"));

    public Task<CommandResultDto> RemoveAt(int index) => dispatcher.InvokeAsync(() =>
        queueService.RemoveAt(index)
            ? CommandResultDto.Ok
            : new CommandResultDto(false, "That item is gone already"));

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
