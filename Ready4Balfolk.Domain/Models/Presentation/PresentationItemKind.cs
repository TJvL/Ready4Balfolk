namespace Ready4Balfolk.Domain.Models.Presentation;

/// <summary>What a presentation surface is being asked to show.</summary>
/// <remarks>
/// A kind rather than a rendered string, because the two surfaces localize differently: the desktop
/// window reads the UI resources, and the browser has its own strings.
/// </remarks>
public enum PresentationItemKind
{
    /// <summary>Nothing to show.</summary>
    None,

    /// <summary>A track, with a dance, an artist and a title.</summary>
    Track,

    /// <summary>An announcement, whose text is the primary line.</summary>
    Message,

    /// <summary>A timed pause. The surface supplies its own label.</summary>
    Delay,

    /// <summary>
    /// The moment between two dances, which the DJ asked for once rather than queued.
    /// </summary>
    /// <remarks>
    /// Its own kind rather than a delay, because a floor reading the screen is being told that the
    /// music has not stopped: this one ends by itself, and the dance behind it is already named.
    /// </remarks>
    Gap,

    /// <summary>An open-ended stop. The surface supplies its own label.</summary>
    Stop,

    /// <summary>The music that ends the evening. The surface supplies its own label.</summary>
    EndOfNight
}
