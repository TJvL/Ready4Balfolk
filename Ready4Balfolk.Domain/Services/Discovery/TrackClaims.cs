using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Discovery.Claims;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Asks everything that can speak about a file what it has to say, and decides nothing.</summary>
/// <remarks>
/// <para>
/// Collecting and deciding are separate so that "what the file offered" survives the decision. A
/// claim that lost is the only way a person can see why a field reads as it does, and a claim the
/// dance list does not recognise is the thing that parks a track in review, so neither may be
/// dropped here.
/// </para>
/// <para>
/// Claims of one field come out in the order they are trusted within a tier: a pattern, then a
/// folder role, then the tags, then the file name. The tier itself is the claim's
/// <see cref="ClaimTrust"/>, not its position, so an observed dance found in a tag can come out
/// before a declared tag order and still lose to it. Nothing is read out of a folder name or a file
/// name field unless the user declared that it means something, because what a level or a field
/// means is not a thing a library can be asked.
/// </para>
/// </remarks>
public static class TrackClaims
{
    /// <summary>Everything said about a file, by everything that said anything.</summary>
    /// <param name="evidence">What the file offered.</param>
    /// <param name="index">The user's dance list, which is the only vocabulary any of this has.</param>
    /// <param name="declared">The rules the user stated, compiled. Undeclared by default.</param>
    /// <param name="folderDance">
    /// The dance the rest of the folder turned out to be, when it agreed on one.
    /// </param>
    public static IEnumerable<Claim> Collect(
        TrackEvidence evidence,
        DanceListIndex index,
        DeclaredDiscovery? declared = null,
        string? folderDance = null)
    {
        declared ??= DeclaredDiscovery.Undeclared;

        IClaimDiscovery[] discoveries =
        [
            // The order is the trust order inside a tier: the first usable claim of a field answers
            // it, so a pattern beats a folder role, and both beat the tag fields.
            new PatternClaimDiscovery(declared),
            new FolderClaimDiscovery(declared),
            new DanceClaimDiscovery(index, declared),
            new TagClaimDiscovery(declared),
            new FilenameTitleDiscovery()
        ];

        return discoveries.SelectMany(discovery => discovery.CollectClaims(evidence, folderDance));
    }
}
