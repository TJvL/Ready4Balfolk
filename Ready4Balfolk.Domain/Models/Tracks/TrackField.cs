namespace Ready4Balfolk.Domain.Models.Tracks;

/// <summary>A field of a track that something can make a claim about.</summary>
/// <remarks>
/// <para>
/// Every one of these must be answered before a track is in the library, and every one of them is
/// reviewable however confident discovery was: a confidently wrong artist is worse than a blank
/// one, because nothing ever draws attention to it.
/// </para>
/// <para>
/// The numbers are written into the library index and are the identity of the member, not its
/// position. They are pinned so a member added here can never be added in front of one: the
/// approvals are the only rows in that index nobody can recompute, and a shift would silently turn
/// every title the DJ has answered into an artist.
/// </para>
/// </remarks>
public enum TrackField
{
    Dance = 0,

    Artist = 1,

    Title = 2
}
