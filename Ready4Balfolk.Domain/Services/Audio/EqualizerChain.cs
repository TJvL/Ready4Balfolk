using ManagedBass;
using ManagedBass.Fx;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Services.Audio;

/// <summary>
/// The BASS_FX effect handles making up one channel's equalizer.
/// </summary>
/// <remarks>
/// BASS has no mixer here: every track is its own stream and the handle is swapped on advance, so
/// the chain is built per channel and travels with the handle. Effects are freed with the stream,
/// so nothing has to be torn down explicitly when a channel goes away.
/// </remarks>
internal sealed class EqualizerChain
{
    /// <summary>Q of a Butterworth high pass, which is maximally flat with no resonant peak.</summary>
    private const double LowCutQ = 0.707;

    /// <summary>Matches the roughly 1.3 octave spacing of the band centres.</summary>
    private const double PeakingBandwidthOctaves = 1.3;

    /// <summary>Shelf slope. Gentle enough to sound like tone control rather than a filter.</summary>
    private const double ShelfSlope = 0.7;

    private readonly int _channel;
    private readonly int _sampleRate;
    private readonly int _lowShelfHandle;
    private readonly int _peakingHandle;
    private readonly int _highShelfHandle;

    private int _lowCutHandle;

    private EqualizerChain(int channel, int sampleRate, int lowShelfHandle, int peakingHandle, int highShelfHandle)
    {
        _channel = channel;
        _sampleRate = sampleRate;
        _lowShelfHandle = lowShelfHandle;
        _peakingHandle = peakingHandle;
        _highShelfHandle = highShelfHandle;
    }

    /// <summary>
    /// Allocates the shelving and peaking effects on a channel. They stay allocated for the life of
    /// the stream: turning the equalizer off sets every gain to 0 dB, which is transparent, rather
    /// than adding and removing effects underneath a playing track.
    /// </summary>
    public static EqualizerChain? TryCreate(int channel)
    {
        var sampleRate = Bass.ChannelGetInfo(channel).Frequency;

        var lowShelf = Bass.ChannelSetFX(channel, EffectType.BQF, 2);
        if (lowShelf == 0)
        {
            return null;
        }

        var peaking = Bass.ChannelSetFX(channel, EffectType.PeakEQ, 1);
        if (peaking == 0)
        {
            Bass.ChannelRemoveFX(channel, lowShelf);
            return null;
        }

        var highShelf = Bass.ChannelSetFX(channel, EffectType.BQF, 0);
        if (highShelf == 0)
        {
            Bass.ChannelRemoveFX(channel, lowShelf);
            Bass.ChannelRemoveFX(channel, peaking);
            return null;
        }

        return new EqualizerChain(channel, sampleRate, lowShelf, peaking, highShelf);
    }

    public void Apply(EqualizerSettings equalizerSettings)
    {
        var gains = equalizerSettings.BandGains;
        var active = equalizerSettings.Enabled;
        var centers = EqualizerSettings.BandCenterFrequencies;
        var lastBand = centers.Count - 1;

        ApplyShelf(_lowShelfHandle, BQFType.LowShelf, centers[0], active ? gains[0] : 0);

        // One PeakEQ handle carries every peaking band, addressed by lBand.
        for (var band = 1; band < lastBand; band++)
        {
            Bass.FXSetParameters(_peakingHandle, new PeakEQParameters
            {
                lBand = band - 1,
                fCenter = (float)ClampCenter(centers[band]),
                fGain = (float)(active ? gains[band] : 0),
                fBandwidth = (float)PeakingBandwidthOctaves,
                fQ = 0,
                lChannel = FXChannelFlags.All
            });
        }

        ApplyShelf(_highShelfHandle, BQFType.HighShelf, centers[lastBand], active ? gains[lastBand] : 0);

        ApplyLowCut(equalizerSettings);

        // Applied at playback rather than in the sample data, so it is the last thing before the
        // device and is what stops a boosted band clipping the output.
        var preamp = active ? Math.Pow(10, equalizerSettings.PreampDecibels / 20) : 1.0;
        Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, preamp);
    }

    private void ApplyShelf(int handle, BQFType filter, int center, double gain)
    {
        Bass.FXSetParameters(handle, new BQFParameters
        {
            lFilter = filter,
            fCenter = (float)ClampCenter(center),
            fGain = (float)gain,
            fBandwidth = 0,
            fQ = 0,
            fS = (float)ShelfSlope,
            lChannel = FXChannelFlags.All
        });
    }

    /// <summary>
    /// A high pass has no neutral setting, so unlike the gain bands it is genuinely added and
    /// removed. That is safe for a BASS_FX effect, which is an ordinary DSP insertion.
    /// </summary>
    private void ApplyLowCut(EqualizerSettings equalizerSettings)
    {
        var wanted = equalizerSettings is { Enabled: true, LowCutEnabled: true };

        if (!wanted)
        {
            if (_lowCutHandle != 0)
            {
                Bass.ChannelRemoveFX(_channel, _lowCutHandle);
                _lowCutHandle = 0;
            }

            return;
        }

        if (_lowCutHandle == 0)
        {
            _lowCutHandle = Bass.ChannelSetFX(_channel, EffectType.BQF, 3);

            if (_lowCutHandle == 0)
            {
                return;
            }
        }

        Bass.FXSetParameters(_lowCutHandle, new BQFParameters
        {
            lFilter = BQFType.HighPass,
            fCenter = (float)ClampCenter(equalizerSettings.LowCutHertz),
            fGain = 0,
            // Bandwidth takes priority over Q and defaults to 1, so it has to be zeroed for the
            // Butterworth Q above to be the thing that shapes the filter.
            fBandwidth = 0,
            fQ = (float)LowCutQ,
            fS = 0,
            lChannel = FXChannelFlags.All
        });
    }

    /// <summary>
    /// BASS_FX rejects a centre at or above half the sample rate, which the 16 kHz band would hit
    /// on anything sampled below 32 kHz.
    /// </summary>
    private double ClampCenter(double center)
    {
        var ceiling = _sampleRate / 2.0 * 0.95;
        return ceiling > 1 ? Math.Clamp(center, 1, ceiling) : center;
    }
}
