using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Models.Presentation;

/// <summary>One queue item reduced to what a presentation surface draws.</summary>
/// <param name="Kind">How the surface should render it.</param>
/// <param name="Primary">
/// The large line: a dance name for a track, the announcement for a message, and empty for a delay
/// or a stop, where the surface supplies its own localized label.
/// </param>
/// <param name="Artist">Track artist, empty for every other kind.</param>
/// <param name="Title">Track title, empty for every other kind and for a track that has none.</param>
/// <param name="Duration">
/// How long the room is being asked to wait: a delay's length, or a message's if the DJ set one.
/// Null for a track, a gap, a stop, the end of the night, or a message with no timer on it.
/// </param>
/// <param name="Id">
/// Which queue item this is, or null for nothing. Drawn by no surface: it is what lets a phone
/// say which dance it was showing when its Skip was pressed.
/// </param>
public sealed record PresentationItem(
    PresentationItemKind Kind,
    string Primary,
    string Artist,
    string Title,
    TimeSpan? Duration = null,
    QueueItemId? Id = null)
{
    /// <summary>Nothing to show.</summary>
    public static readonly PresentationItem None =
        new(PresentationItemKind.None, string.Empty, string.Empty, string.Empty);

    /// <summary>Whether there is anything at all to draw.</summary>
    public bool HasContent => Kind is not PresentationItemKind.None;
}
