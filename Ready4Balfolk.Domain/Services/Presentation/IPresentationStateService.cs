using Ready4Balfolk.Domain.Models.Presentation;

namespace Ready4Balfolk.Domain.Services.Presentation;

/// <summary>
/// Reduces the queue and the player to what a presentation surface draws, once, for every surface.
/// </summary>
/// <remarks>
/// The desktop window and the browser have to agree on all six pictures the display can show, five
/// of which are not a track. Mapping queue items to those pictures in a view model would mean
/// writing the same switch again for the web, and drifting the first time an item type is added.
/// </remarks>
public interface IPresentationStateService
{
    /// <summary>What is on screen right now.</summary>
    PresentationState Current { get; }

    /// <summary>Fires whenever the current or next item, or the playing flag, changes.</summary>
    IObservable<PresentationState> WhenStateChanged { get; }

    /// <summary>Fires as playback advances, at the player's own rate of about ten a second.</summary>
    IObservable<PresentationProgress> WhenProgressChanged { get; }
}
