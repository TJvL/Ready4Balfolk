namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Where a suggestion about a track's dance came from.</summary>
/// <remarks>
/// These have to be genuinely independent of one another, because agreement between two of them is
/// what makes an answer trustworthy.
/// </remarks>
public enum DanceEvidenceSource
{
    /// <summary>A name from the list, found anywhere in the file's name.</summary>
    FileName,

    /// <summary>A name from the list, found in the tags written into the file.</summary>
    Tags,

    /// <summary>What the rest of the folder the file sits in turned out to be.</summary>
    Folder
}
