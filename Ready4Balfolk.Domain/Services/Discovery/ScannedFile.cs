using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>A file that was actually opened, and what was made of it.</summary>
/// <remarks>
/// <see cref="Resolution"/> is settable because folder agreement revisits it once the folder is
/// complete, which is the one thing that cannot be decided a file at a time.
/// </remarks>
public sealed record ScannedFile(IFileInfo File, TrackEvidence Evidence, TrackResolution Resolution)
{
    public TrackResolution Resolution { get; set; } = Resolution;
}
