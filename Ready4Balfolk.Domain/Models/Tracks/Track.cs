using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Models.Tracks;

public sealed record Track(string Dance, string Artist, string Title, IFileInfo FileInfo, TimeSpan Length, AudioFormat Format)
{
    /// <summary>What the file itself claims, before the dance list had a say.</summary>
    public string OriginalDance { get; init; } = Dance;

    /// <summary>
    /// The dance this track resolved to, or null when the list does not know the name.
    /// </summary>
    /// <remarks>
    /// The slug, not a name: it survives respelling, reordering and renaming, so a track keeps
    /// pointing at the same dance however the user edits their list. <see cref="Dance"/> is only
    /// what that slug is currently displayed as.
    /// </remarks>
    public string? DanceSlug { get; init; }

    /// <summary>How much likelier a random pick is to land on this track than on another of its dance.</summary>
    /// <remarks>
    /// What a person set on the track, stored beside its approvals in the library index rather than
    /// in the file's tags, so it follows the audio through a retag or a rename.
    /// </remarks>
    public double Likelihood
    {
        get;
        init => field = TrackLikelihood.Normalize(value);
    } = TrackLikelihood.Usual;

    /// <summary>This track's own equalizer, or null when it has never had one.</summary>
    /// <remarks>
    /// <see cref="EqualizerSettings.Enabled"/> says whether it is in use: switched off, the curve
    /// is kept so it can be switched back on, and the global equalizer plays the track as it plays
    /// everything else. Either way it only ever counts while the global equalizer is switched on.
    /// Stored beside the approvals in the library index, like the likelihood.
    /// </remarks>
    public EqualizerSettings? Equalizer { get; init; }

    /// <summary>Whether this track plays through an equalizer of its own when the global one is on.</summary>
    public bool HasOwnEqualizer => Equalizer is { Enabled: true };
}
