using System.Diagnostics;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Views.Equalizer;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class EqualizerViewModelTests : IDisposable
{
    private readonly IAudioPlaybackService _audio = Substitute.For<IAudioPlaybackService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly List<ApplicationSettings> _saved = [];
    private readonly EqualizerViewModel _sut;

    private ApplicationSettings _stored = new();

    public EqualizerViewModelTests()
    {
        _audio.IsEqualizerAvailable.Returns(true);
        _audio.SetEqualizerAsync(Arg.Any<EqualizerSettings>()).Returns(Task.CompletedTask);

        _settingsStore.Current.Returns(_ => _stored);
        _settingsStore
            .UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>())
            .Returns(call =>
            {
                var transform = call.Arg<Func<ApplicationSettings, ApplicationSettings>>()!;
                _stored = transform(_stored);

                lock (_saved)
                {
                    _saved.Add(_stored);
                }

                return Task.CompletedTask;
            });

        _sut = new EqualizerViewModel(_audio, _settingsStore, Substitute.For<ILoggerService>());
    }

    [Fact]
    public void Construction_RestoresTheStoredEqualizer()
    {
        _stored = new ApplicationSettings() with
        {
            EqualizerOrNull = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -4 }
        };

        using var sut = new EqualizerViewModel(_audio, _settingsStore, Substitute.For<ILoggerService>());

        Assert.True(sut.Enabled);
        Assert.Equal(-4, sut.PreampDecibels);
        Assert.Equal(EqualizerSettings.BandCenterFrequencies.Count, sut.Bands.Count);
    }

    [Fact]
    public void Construction_DoesNotSaveOrReapply()
    {
        _audio.DidNotReceive().SetEqualizerAsync(Arg.Any<EqualizerSettings>());
        Assert.Empty(SavedSnapshot());
    }

    // The audio has to follow the control without waiting for the save throttle.
    [Fact]
    public void EnablingApppliesToAudioImmediately()
    {
        _sut.Enabled = true;

        _audio.Received().SetEqualizerAsync(Arg.Is<EqualizerSettings>(settings => settings!.Enabled));
    }

    [Fact]
    public async Task EnablingIsPersisted()
    {
        _sut.Enabled = true;

        var saved = await WaitForSaveAsync();

        Assert.True(saved.Equalizer.Enabled);
    }

    [Fact]
    public async Task BandGainIsPersistedAtItsIndex()
    {
        _sut.Bands[2].Gain = -6;

        var saved = await WaitForSaveAsync();

        Assert.Equal(-6, saved.Equalizer.BandGains[2]);
        Assert.Equal(0, saved.Equalizer.BandGains[0]);
    }

    [Fact]
    public async Task LowCutIsPersisted()
    {
        _sut.LowCutEnabled = true;
        _sut.LowCutHertz = 65;

        var saved = await WaitForSaveAsync();

        Assert.True(saved.Equalizer.LowCutEnabled);
        Assert.Equal(65, saved.Equalizer.LowCutHertz);
    }

    [Fact]
    public async Task ResetToFlat_ClearsEverythingButLeavesTheEqualizerEnabled()
    {
        _sut.Enabled = true;
        _sut.Bands[1].Gain = 9;
        _sut.PreampDecibels = -5;
        _sut.LowCutEnabled = true;

        _sut.ResetToFlatCommand.Execute().Subscribe();

        var saved = await WaitForSaveAsync(settings => settings.Equalizer.IsFlat);

        Assert.True(saved.Equalizer.Enabled);
        Assert.True(saved.Equalizer.IsFlat);
        Assert.All(_sut.Bands, band => Assert.Equal(0, band.Gain));
    }

    private List<ApplicationSettings> SavedSnapshot()
    {
        lock (_saved)
        {
            return [.. _saved];
        }
    }

    /// <summary>Waits out the view model's save throttle rather than sleeping a fixed period.</summary>
    private async Task<ApplicationSettings> WaitForSaveAsync(Func<ApplicationSettings, bool>? predicate = null)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var match = SavedSnapshot().LastOrDefault(settings => predicate?.Invoke(settings) ?? true);

            if (match != null)
            {
                return match;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("The equalizer was never saved.");
    }

    public void Dispose() => _sut.Dispose();
}
