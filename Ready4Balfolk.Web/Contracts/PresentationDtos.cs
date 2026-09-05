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
    PresentationItemDto Behind,
    bool IsPlaying,
    double ElapsedSeconds,
    double DurationSeconds)
{
    public static PresentationSnapshotDto From(PresentationState state, PresentationProgress progress) =>
        new(
            PresentationItemDto.From(state.Current),
            PresentationItemDto.From(state.Next),
            PresentationItemDto.From(state.Behind),
            state.IsPlaying,
            progress.Elapsed.TotalSeconds,
            progress.Duration.TotalSeconds);
}

/// <summary>One queue row on the remote.</summary>
/// <remarks>
/// <c>IsAuto</c> marks an automatically added track, which the desktop refuses to remove or reorder;
/// the remote disables its actions for the same reason.
///
/// <c>Id</c> rather than a position, because the list a phone is looking at is up to half a second
/// behind the queue and a dance ending renumbers every row in it. The position a row is drawn at is
/// where it sits in this list; what the phone sends back is which row it meant.
/// </remarks>
public sealed record QueueEntryDto(
    string Id,
    string Kind,
    string Primary,
    string Artist,
    string Title,
    double? DurationSeconds,
    bool IsAuto)
{
    public static QueueEntryDto From(IQueueItem item)
    {
        var mapped = PresentationStateService.Map(item);
        return new QueueEntryDto(
            item.Id.ToString(),
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
/// <remarks>
/// <paramref name="QueueChanged"/> is the refusal the phone has to word for itself. The reason a
/// rule gives is written on the computer and travels as text; "the row you tapped is not there any
/// more" is not a rule saying no, it is this phone's list being out of date, and the page says so in
/// its own language and redraws. A transport command about a dance that has since ended is the same
/// thing about the top of the screen rather than the list, and comes back the same way.
/// </remarks>
public sealed record CommandResultDto(bool Accepted, string? Reason = null, bool QueueChanged = false)
{
    public static readonly CommandResultDto Ok = new(true);

    /// <summary>The row is gone: whoever asked was reading a queue that has since moved on.</summary>
    public static readonly CommandResultDto Stale = new(false, null, true);
}
