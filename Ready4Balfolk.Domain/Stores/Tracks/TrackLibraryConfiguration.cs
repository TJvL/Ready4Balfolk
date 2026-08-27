using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Stores.Tracks;

/// <summary>Everything the track store needs to know about, in one value.</summary>
/// <remarks>
/// <para>
/// This was three write-only properties, each starting its own fire-and-forget work from its setter.
/// Setting them in the wrong order made the store scan the whole library twice, once under the old
/// rules and again under the new ones, which is why the subscriptions in App had a comment
/// explaining that discovery had to be subscribed before the music directory. Handing all three
/// over together means that ordering hazard cannot be expressed, rather than being documented.
/// </para>
/// <para>
/// A path rather than a directory object, so the record has value equality and
/// <c>DistinctUntilChanged</c> works on it. The store builds the directory with its own filesystem.
/// </para>
/// </remarks>
public sealed record TrackLibraryConfiguration(
    string? MusicDirectoryPath,
    DiscoverySettings Discovery,
    bool AllowDancesOutsideTheList)
{
    public static TrackLibraryConfiguration Undeclared { get; } =
        new(null, DiscoverySettings.Undeclared, false);
}
