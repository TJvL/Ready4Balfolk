using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>
/// Global output equalizer, for correcting a room or a PA when the app is the only place the
/// sound can be shaped. Owns its own clamping so that every construction path, including
/// deserialization of a hand-edited settings file, yields values BASS will accept.
/// </summary>
public sealed record EqualizerSettings
{
    /// <summary>BASS_FX accepts -15 to +15 dB for both peaking and shelving filters.</summary>
    public const double MinimumGainDecibels = -15.0;

    public const double MaximumGainDecibels = 15.0;

    public const double MinimumLowCutHertz = 20.0;
    public const double MaximumLowCutHertz = 200.0;

    /// <summary>
    /// Fixed centres, roughly 1.3 octaves apart. The outer two are shelving filters and the five
    /// between them are peaking, so the end sliders move the whole bottom and top rather than a
    /// narrow bump that misses most of it.
    /// </summary>
    public static readonly IReadOnlyList<int> BandCenterFrequencies = [63, 160, 400, 1000, 2500, 6300, 16000];

    public static readonly EqualizerSettings Flat = new();

    public bool Enabled { get; init; }

    public bool LowCutEnabled { get; init; }

    /// <summary>One gain in dB per entry in <see cref="BandCenterFrequencies"/>.</summary>
    public IReadOnlyList<double> BandGains
    {
        get;
        init => field = NormalizeGains(value);
    } = NormalizeGains(null);

    /// <summary>Output trim, mainly to buy back headroom when bands are boosted.</summary>
    public double PreampDecibels
    {
        get;
        init => field = ClampGain(value);
    }

    /// <summary>Corner frequency of the high pass, applied only when <see cref="LowCutEnabled"/>.</summary>
    public double LowCutHertz
    {
        get;
        init => field = Math.Clamp(double.IsNaN(value) ? MinimumLowCutHertz : value,
            MinimumLowCutHertz, MaximumLowCutHertz);
    } = MinimumLowCutHertz;

    /// <summary>True when no band, preamp or low cut would alter the sound.</summary>
    [JsonIgnore]
    public bool IsFlat => !LowCutEnabled && PreampDecibels == 0 && BandGains.All(gain => gain == 0);

    public EqualizerSettings WithBandGain(int index, double gain)
    {
        if (index < 0 || index >= BandCenterFrequencies.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var gains = BandGains.ToArray();
        gains[index] = ClampGain(gain);
        return this with { BandGains = gains };
    }

    /// <summary>
    /// Pads, truncates and clamps into exactly one gain per band. A settings file written before
    /// this existed, or before the band count last changed, deserializes to null or to the wrong
    /// length rather than to something unusable.
    /// </summary>
    private static double[] NormalizeGains(IReadOnlyList<double>? gains)
    {
        var normalized = new double[BandCenterFrequencies.Count];

        if (gains is null)
        {
            return normalized;
        }

        for (var index = 0; index < normalized.Length && index < gains.Count; index++)
        {
            normalized[index] = ClampGain(gains[index]);
        }

        return normalized;
    }

    private static double ClampGain(double gain) => Math.Clamp(
        double.IsNaN(gain) ? 0 : gain, MinimumGainDecibels, MaximumGainDecibels);

    // The synthesized record equality compares the gain list by reference, which would report two
    // identical equalizers as different and defeat any DistinctUntilChanged upstream.
    public bool Equals(EqualizerSettings? other) =>
        other is not null
        && Enabled == other.Enabled
        && LowCutEnabled == other.LowCutEnabled
        && PreampDecibels.Equals(other.PreampDecibels)
        && LowCutHertz.Equals(other.LowCutHertz)
        && BandGains.SequenceEqual(other.BandGains);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Enabled);
        hash.Add(LowCutEnabled);
        hash.Add(PreampDecibels);
        hash.Add(LowCutHertz);

        foreach (var gain in BandGains)
        {
            hash.Add(gain);
        }

        return hash.ToHashCode();
    }
}
