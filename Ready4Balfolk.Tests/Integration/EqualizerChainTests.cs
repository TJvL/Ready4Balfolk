using ManagedBass;
using ManagedBass.Fx;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// The equalizer as the room hears it: the effects are put on a real stream and the audio that
/// comes out of it is measured.
/// </summary>
/// <remarks>
/// Decoded rather than played, so this needs no sound card and no clock: BASS runs the same effect
/// chain when it is asked for the samples as when it sends them to a device, and the numbers below
/// are the same on any machine because they come out of the same file and the same filters.
///
/// Asserting that the handles were allocated would prove almost nothing, since a chain that is
/// attached to the wrong channel, or is being handed gains it never applies, allocates just as
/// happily and leaves the hall flat.
/// </remarks>
[Collection(ProcessWideAudioState.Name)]
public sealed class EqualizerChainTests : IDisposable
{
    /// <summary>Every band pulled all the way down, which is as far as BASS_FX goes.</summary>
    private static readonly EqualizerSettings EverythingDown = new()
    {
        Enabled = true,
        BandGains = [.. EqualizerSettings.BandCenterFrequencies.Select(_ => EqualizerSettings.MinimumGainDecibels)]
    };

    private readonly string _root;
    private readonly string _path;

    public EqualizerChainTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _path = Path.Combine(_root, "scale.mp3");

        using (var source = typeof(EqualizerChainTests).Assembly
                                .GetManifestResourceStream("scale.mp3")
                            ?? throw new InvalidOperationException("Embedded audio 'scale.mp3' is missing."))
        using (var destination = File.Create(_path))
        {
            source.CopyTo(destination);
        }

        // The "no sound" device, as the smoke test and the application under CI use it.
        Bass.Init(0);

        // Both of these are what the playback service does at startup, and the chain is built on
        // top of them: BASS_FX has to have been loaded before an add-on effect can go on a
        // channel at all, and the whole chain runs in floating point.
        _ = BassFx.Version;
        Bass.FloatingPointDSP = true;
    }

    [Fact]
    public void AChain_IsBuiltOnARealStream()
    {
        var channel = OpenForDecoding();

        var chain = EqualizerChain.TryCreate(channel);

        Assert.NotNull(chain);
        Bass.StreamFree(channel);
    }

    /// <summary>
    /// A chain sitting on a channel with nothing turned up is not something the room can hear.
    /// </summary>
    /// <remarks>
    /// This is what lets the chain be built once per stream and left there: a track played with
    /// the equalizer switched off has to sound like the file, not like a filter set to nearly
    /// nothing.
    /// </remarks>
    [Fact]
    public void AFlatChain_LeavesTheAudioAsItWas()
    {
        var untouched = LoudnessWith(null);

        var through = LoudnessWith(EqualizerSettings.Flat);

        Assert.Equal(untouched, through, untouched * 0.001);
    }

    [Fact]
    public void PullingEveryBandDown_IsHeard()
    {
        var untouched = LoudnessWith(null);

        var cut = LoudnessWith(EverythingDown);

        // Six decibels is half the loudness and a fraction of the fifteen every band was cut by,
        // so this says the gains reached the audio without pinning the test to a filter shape.
        Assert.True(cut < untouched / 2, $"Cutting every band left the audio at {cut} against {untouched}.");
    }

    /// <summary>
    /// Switched off means every gain at 0 dB rather than the effects torn off a playing channel,
    /// so the sliders being down has to stop mattering the moment the switch does.
    /// </summary>
    [Fact]
    public void TheSwitchBeingOff_IsTransparentEvenWithEverySliderDown()
    {
        var untouched = LoudnessWith(null);

        var off = LoudnessWith(EverythingDown with { Enabled = false });

        Assert.Equal(untouched, off, untouched * 0.001);
    }

    /// <summary>
    /// The low cut is the one thing that is genuinely added and removed, having no neutral setting
    /// of its own, so it has to be gone again when it is switched off.
    /// </summary>
    [Fact]
    public void TheLowCut_IsHeardWhileItIsOnAndGoneWhenItIsNot()
    {
        var untouched = LoudnessWith(null);

        var withLowCut = LoudnessWith(new EqualizerSettings
        {
            Enabled = true,
            LowCutEnabled = true,
            LowCutHertz = EqualizerSettings.MaximumLowCutHertz
        });

        // Left behind after being switched off, it would still be taking the bottom out of the
        // sound, which is what the second measurement is against.
        var afterItWasSwitchedOff = LoudnessWith(
            new EqualizerSettings
            {
                Enabled = true,
                LowCutEnabled = true,
                LowCutHertz = EqualizerSettings.MaximumLowCutHertz
            },
            then: new EqualizerSettings { Enabled = true, LowCutEnabled = false });

        Assert.True(withLowCut < untouched, $"The low cut left the audio at {withLowCut} against {untouched}.");
        Assert.Equal(untouched, afterItWasSwitchedOff, untouched * 0.001);
    }

    /// <summary>
    /// The preamp is a trim on the channel rather than a filter, which is what makes it the last
    /// thing before the device and the only thing that can buy back the headroom a boost eats.
    /// </summary>
    [Fact]
    public void ThePreamp_TrimsTheChannelItself()
    {
        var channel = OpenForDecoding();
        var chain = EqualizerChain.TryCreate(channel);
        Assert.NotNull(chain);

        chain.Apply(new EqualizerSettings { Enabled = true, PreampDecibels = -6 });
        Bass.ChannelGetAttribute(channel, ChannelAttribute.Volume, out var trimmed);

        chain.Apply(new EqualizerSettings { Enabled = false, PreampDecibels = -6 });
        Bass.ChannelGetAttribute(channel, ChannelAttribute.Volume, out var switchedOff);

        Bass.StreamFree(channel);

        Assert.Equal(Math.Pow(10, -6 / 20.0), trimmed, 0.001);
        Assert.Equal(1.0, switchedOff, 0.001);
    }

    /// <summary>
    /// How loud the whole file comes out through a chain carrying <paramref name="settings" />, or
    /// with no chain on it at all when there are none.
    /// </summary>
    private double LoudnessWith(EqualizerSettings? settings, EqualizerSettings? then = null)
    {
        var channel = OpenForDecoding();

        if (settings is not null)
        {
            var chain = EqualizerChain.TryCreate(channel);
            Assert.NotNull(chain);
            chain.Apply(settings);

            if (then is not null)
            {
                chain.Apply(then);
            }
        }

        var buffer = new float[4096];
        double sum = 0;
        long samples = 0;

        while (true)
        {
            var read = Bass.ChannelGetData(channel, buffer, (buffer.Length * sizeof(float)) | (int)DataFlags.Float);

            if (read <= 0)
            {
                break;
            }

            var count = read / sizeof(float);
            for (var index = 0; index < count; index++)
            {
                sum += buffer[index] * (double)buffer[index];
            }

            samples += count;
        }

        Bass.StreamFree(channel);

        Assert.True(samples > 0, "Nothing was decoded out of the embedded audio.");
        return Math.Sqrt(sum / samples);
    }

    /// <summary>
    /// The file as a decoding channel: asked for its samples rather than playing them, which is
    /// what lets this be measured with nothing waited on and no device involved.
    /// </summary>
    private int OpenForDecoding()
    {
        var channel = Bass.CreateStream(_path, 0, 0, BassFlags.Decode | BassFlags.Float);
        Assert.True(channel != 0, $"Could not open the embedded audio: {Bass.LastError}");
        return channel;
    }

    public void Dispose()
    {
        Bass.Free();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
