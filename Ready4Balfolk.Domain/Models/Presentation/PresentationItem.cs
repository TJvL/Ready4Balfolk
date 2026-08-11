namespace Ready4Balfolk.Domain.Models.Presentation;

/// <summary>One queue item reduced to what a presentation surface draws.</summary>
/// <param name="Kind">How the surface should render it.</param>
/// <param name="Primary">
/// The large line: a dance name for a track, the announcement for a message, and empty for a delay
/// or a stop, where the surface supplies its own localized label.
/// </param>
/// <param name="Artist">Track artist, empty for every other kind.</param>
/// <param name="Title">Track title, empty for every other kind and for a track that has none.</param>
public sealed record PresentationItem(
    PresentationItemKind Kind,
    string Primary,
    string Artist,
    string Title)
{
    /// <summary>Nothing to show.</summary>
    public static readonly PresentationItem None =
        new(PresentationItemKind.None, string.Empty, string.Empty, string.Empty);

    /// <summary>Whether there is anything at all to draw.</summary>
    public bool HasContent => Kind is not PresentationItemKind.None;

    /// <summary>Whether the artist and title line should be drawn.</summary>
    public bool HasSubtitle => Kind is PresentationItemKind.Track && Artist.Length > 0;
}
