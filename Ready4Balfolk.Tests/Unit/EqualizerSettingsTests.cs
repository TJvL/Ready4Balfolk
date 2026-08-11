using System.Text.Json;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Tests.Unit;

public sealed class EqualizerSettingsTests
{
    private static int BandCount => EqualizerSettings.BandCenterFrequencies.Count;

    [Fact]
    public void Default_IsFlatAndFullyPopulated()
    {
        var settings = EqualizerSettings.Flat;

        Assert.False(settings.Enabled);
        Assert.True(settings.IsFlat);
        Assert.Equal(BandCount, settings.BandGains.Count);
        Assert.All(settings.BandGains, gain => Assert.Equal(0, gain));
    }

    // A settings file written before the equalizer existed deserializes the gains to null.
    [Fact]
    public void BandGains_Null_BecomesOneZeroPerBand()
    {
        var settings = new EqualizerSettings { BandGains = null! };

        Assert.Equal(BandCount, settings.BandGains.Count);
        Assert.All(settings.BandGains, gain => Assert.Equal(0, gain));
    }

    // A file written when the band count was different must not produce a short list, since the
    // audio chain indexes every band.
    [Fact]
    public void BandGains_TooShort_IsPaddedWithZeros()
    {
        var settings = new EqualizerSettings { BandGains = [3, -4] };

        Assert.Equal(BandCount, settings.BandGains.Count);
        Assert.Equal(3, settings.BandGains[0]);
        Assert.Equal(-4, settings.BandGains[1]);
        Assert.Equal(0, settings.BandGains[2]);
    }

    [Fact]
    public void BandGains_TooLong_IsTruncated()
    {
        var gains = Enumerable.Repeat(2.0, BandCount + 5).ToArray();

        var settings = new EqualizerSettings { BandGains = gains };

        Assert.Equal(BandCount, settings.BandGains.Count);
    }

    [Theory]
    [InlineData(40.0, 15.0)]
    [InlineData(-40.0, -15.0)]
    [InlineData(6.5, 6.5)]
    [InlineData(double.NaN, 0.0)]
    public void BandGains_AreClampedToWhatBassAccepts(double input, double expected)
    {
        var settings = new EqualizerSettings { BandGains = [input] };

        Assert.Equal(expected, settings.BandGains[0]);
    }

    [Theory]
    [InlineData(99.0, 15.0)]
    [InlineData(-99.0, -15.0)]
    [InlineData(double.NaN, 0.0)]
    public void Preamp_IsClamped(double input, double expected) =>
        Assert.Equal(expected, new EqualizerSettings { PreampDecibels = input }.PreampDecibels);

    [Theory]
    [InlineData(5.0, EqualizerSettings.MinimumLowCutHertz)]
    [InlineData(5000.0, EqualizerSettings.MaximumLowCutHertz)]
    [InlineData(55.0, 55.0)]
    [InlineData(double.NaN, EqualizerSettings.MinimumLowCutHertz)]
    public void LowCut_IsClamped(double input, double expected) =>
        Assert.Equal(expected, new EqualizerSettings { LowCutHertz = input }.LowCutHertz);

    [Fact]
    public void WithBandGain_ReplacesOnlyThatBand()
    {
        var settings = EqualizerSettings.Flat.WithBandGain(2, -6);

        Assert.Equal(-6, settings.BandGains[2]);
        Assert.Equal(0, settings.BandGains[0]);
        Assert.Equal(BandCount, settings.BandGains.Count);
    }

    [Fact]
    public void WithBandGain_ClampsAndDoesNotMutateTheOriginal()
    {
        var original = EqualizerSettings.Flat;

        var updated = original.WithBandGain(0, 100);

        Assert.Equal(15, updated.BandGains[0]);
        Assert.Equal(0, original.BandGains[0]);
    }

    [Fact]
    public void WithBandGain_OutOfRange_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EqualizerSettings.Flat.WithBandGain(BandCount, 0));

    [Fact]
    public void IsFlat_FalseOnceAnythingIsSet()
    {
        Assert.False(EqualizerSettings.Flat.WithBandGain(0, 1).IsFlat);
        Assert.False((EqualizerSettings.Flat with { PreampDecibels = -3 }).IsFlat);
        Assert.False((EqualizerSettings.Flat with { LowCutEnabled = true }).IsFlat);
    }

    // The gain list would otherwise compare by reference, so two identical equalizers would look
    // different to anything deduplicating changes.
    [Fact]
    public void Equality_ComparesGainsByValue()
    {
        var one = EqualizerSettings.Flat.WithBandGain(1, 4);
        var two = EqualizerSettings.Flat.WithBandGain(1, 4);

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.NotEqual(one, EqualizerSettings.Flat.WithBandGain(1, 5));
    }

    [Fact]
    public void ApplicationSettings_WithoutAnEqualizer_ReadsAsFlat() =>
        Assert.Equal(EqualizerSettings.Flat, new ApplicationSettings().Equalizer);

    // Equalizer is a computed view over EqualizerOrNull. Serializing both wrote the whole thing
    // twice, with only EqualizerOrNull ever read back.
    [Fact]
    public void Serialization_WritesTheEqualizerExactlyOnce()
    {
        var settings = new ApplicationSettings() with
        {
            EqualizerOrNull = EqualizerSettings.Flat with { Enabled = true }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings));

        Assert.True(document.RootElement.TryGetProperty("EqualizerOrNull", out _));
        Assert.False(document.RootElement.TryGetProperty("Equalizer", out _));
        Assert.False(document.RootElement
            .GetProperty("EqualizerOrNull")
            .TryGetProperty("IsFlat", out _));
    }

    [Fact]
    public void Serialization_RoundTripsThroughJson()
    {
        var equalizer = EqualizerSettings.Flat with { Enabled = true, LowCutEnabled = true, LowCutHertz = 55 };
        var settings = new ApplicationSettings() with { EqualizerOrNull = equalizer.WithBandGain(3, -7) };

        var restored = JsonSerializer.Deserialize<ApplicationSettings>(JsonSerializer.Serialize(settings));

        Assert.NotNull(restored);
        Assert.Equal(settings.Equalizer, restored.Equalizer);
        Assert.Equal(-7, restored.Equalizer.BandGains[3]);
        Assert.Equal(55, restored.Equalizer.LowCutHertz);
    }

    [Fact]
    public void ApplicationSettings_RoundTripsAStoredEqualizer()
    {
        var equalizer = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -2 };

        var settings = new ApplicationSettings() with { EqualizerOrNull = equalizer };

        Assert.Equal(equalizer, settings.Equalizer);
    }
}
