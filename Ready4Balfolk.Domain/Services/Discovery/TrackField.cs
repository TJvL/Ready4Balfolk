namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>A field of a track that something can make a claim about.</summary>
/// <remarks>
/// Every one of these must be answered before a track is in the library, and every one of them is
/// reviewable however confident discovery was: a confidently wrong artist is worse than a blank
/// one, because nothing ever draws attention to it.
/// </remarks>
public enum TrackField
{
    Dance,

    Artist,

    Title
}
