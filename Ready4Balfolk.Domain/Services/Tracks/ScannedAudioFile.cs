using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Everything read from an audio file in a single open.</summary>
/// <remarks>
/// Opening a file is the expensive part of a scan, so it happens once and yields the tags, the
/// duration and the content hash together rather than three times over.
/// </remarks>
public sealed record ScannedAudioFile(
    string Dance,
    string Artist,
    string Title,
    TimeSpan Duration,
    AudioFormat Format,
    byte[] ContentHash);
