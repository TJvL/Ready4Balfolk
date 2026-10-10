using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Audio;

/// <summary>The equalizers of tracks that have one of their own, and which of them is playing.</summary>
public interface ITrackEqualizerService
{
    /// <summary>
    /// The library track that is playing through its own equalizer, or null while the global one
    /// is in force. Replayed to new subscribers.
    /// </summary>
    /// <remarks>
    /// Null for anything that is not a library track, for a track whose own equalizer is switched
    /// off or that never had one, and for every track while the global equalizer is switched off,
    /// because that switch takes every effect out of the signal at once. The track is the library's
    /// current one rather than the queue's copy of it, so a curve changed after the track was queued
    /// is the curve this carries.
    /// </remarks>
    IObservable<Track?> WhenInForce { get; }

    Track? InForce { get; }

    /// <summary>Plays this track through this curve at once, without writing it anywhere.</summary>
    /// <remarks>
    /// For a slider being pulled: the sound has to follow the hand, and writing every step of a
    /// drag to the library index is a write per pixel. <see cref="SaveAsync"/> is what keeps it.
    /// </remarks>
    void Hear(Track track, EqualizerSettings equalizer);

    /// <summary>Keeps this curve as the track's own.</summary>
    Task SaveAsync(Track track, EqualizerSettings equalizer);

    /// <summary>Switches the track's own equalizer off, keeping its curve, so the global one plays it.</summary>
    Task BackToGlobalAsync(Track track);
}
