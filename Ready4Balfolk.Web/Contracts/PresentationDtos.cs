using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Presentation;

namespace Ready4Balfolk.Web.Contracts;

/// <summary>One line of the display, as it goes over the wire.</summary>
/// <remarks>
/// <paramref name="Kind"/> travels as its name rather than an ordinal so the browser reads
/// <c>"Delay"</c> instead of <c>3</c>, and so inserting an item type cannot silently renumber it.
/// </remarks>
public sealed record PresentationItemDto(string Kind, string Primary, string Artist, string Title)
{
    public static PresentationItemDto From(PresentationItem item) =>
        new(item.Kind.ToString(), item.Primary, item.Artist, item.Title);
}

/// <summary>Everything a display page draws, in one message.</summary>
public sealed record PresentationSnapshotDto(
    PresentationItemDto Current,
    PresentationItemDto Next,
    bool IsPlaying,
    double ElapsedSeconds,
    double DurationSeconds)
{
    public static PresentationSnapshotDto From(PresentationState state, PresentationProgress progress) =>
        new(
            PresentationItemDto.From(state.Current),
            PresentationItemDto.From(state.Next),
            state.IsPlaying,
            progress.Elapsed.TotalSeconds,
            progress.Duration.TotalSeconds);
}

/// <summary>One queue row on the remote.</summary>
/// <remarks>
/// <c>IsAuto</c> marks an automatically added track, which the desktop refuses to remove or reorder;
/// the remote disables its actions for the same reason.
/// </remarks>
public sealed record QueueEntryDto(
    int Index,
    string Kind,
    string Primary,
    string Artist,
    string Title,
    double? DurationSeconds,
    bool IsAuto)
{
    public static QueueEntryDto From(IQueueItem item, int index)
    {
        var mapped = PresentationStateService.Map(item);
        return new QueueEntryDto(
            index,
            mapped.Kind.ToString(),
            mapped.Primary,
            mapped.Artist,
            mapped.Title,
            item.Duration?.TotalSeconds,
            item is AutoTrackQueueItem);
    }
}

/// <summary>A search result on the remote's find tab.</summary>
public sealed record TrackHitDto(string Id, string Dance, string Artist, string Title, double DurationSeconds);

/// <summary>The result of a command the remote sent.</summary>
public sealed record CommandResultDto(bool Accepted, string? Reason = null)
{
    public static readonly CommandResultDto Ok = new(true);
}
