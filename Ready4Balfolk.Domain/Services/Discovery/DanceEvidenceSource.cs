namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Where a suggestion about a track's dance came from.</summary>
/// <remarks>
/// These have to be genuinely independent of one another, because agreement between two of them is
/// what makes an answer trustworthy. The filename pattern is deliberately not one of them: it reads
/// the same string the filename scan does, so agreeing with it proves nothing.
/// </remarks>
public enum DanceEvidenceSource
{
    /// <summary>A name from the list, found anywhere in the file's name.</summary>
    FileName,

    /// <summary>A name from the list, found in the tags written into the file.</summary>
    Tags,

    /// <summary>What the rest of the album folder turned out to be.</summary>
    Folder
}
