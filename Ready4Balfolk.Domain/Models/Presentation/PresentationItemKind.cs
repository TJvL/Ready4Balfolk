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

    /// <summary>An open-ended stop. The surface supplies its own label.</summary>
    Stop
}
