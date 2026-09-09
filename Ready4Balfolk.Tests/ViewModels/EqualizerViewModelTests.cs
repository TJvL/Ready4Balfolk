using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Views.Equalizer;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class EqualizerViewModelTests : IDisposable
{
    private readonly IAudioPlaybackService _audio = Substitute.For<IAudioPlaybackService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ThrottleClock _throttles = new();
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

        _sut = new EqualizerViewModel(_audio, _settingsStore, Substitute.For<ILoggerService>(), _throttles.Scheduler);
    }

    /// <summary>Spends the 300ms the panel waits before writing, rather than sleeping past it.</summary>
    private void Settle() => _throttles.LetTheThrottlesRunOut();

    [Fact]
    public void Construction_RestoresTheStoredEqualizer()
    {
        _stored = new ApplicationSettings() with
        {
            EqualizerOrNull = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -4 }
        };

        using var sut = new EqualizerViewModel(
            _audio, _settingsStore, Substitute.For<ILoggerService>(), _throttles.Scheduler);

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
    public void EnablingIsPersisted()
    {
        _sut.Enabled = true;
        Settle();

        Assert.True(SavedSnapshot()[^1].Equalizer.Enabled);
    }

    [Fact]
    public void BandGainIsPersistedAtItsIndex()
    {
        _sut.Bands[2].Gain = -6;
        Settle();

        var saved = SavedSnapshot()[^1];

        Assert.Equal(-6, saved.Equalizer.BandGains[2]);
        Assert.Equal(0, saved.Equalizer.BandGains[0]);
    }

    [Fact]
    public void LowCutIsPersisted()
    {
        _sut.LowCutEnabled = true;
        _sut.LowCutHertz = 65;
        Settle();

        var saved = SavedSnapshot()[^1];

        Assert.True(saved.Equalizer.LowCutEnabled);
        Assert.Equal(65, saved.Equalizer.LowCutHertz);
    }

    [Fact]
    public void ResetToFlat_ClearsEverythingButLeavesTheEqualizerEnabled()
    {
        _sut.Enabled = true;
        _sut.Bands[1].Gain = 9;
        _sut.PreampDecibels = -5;
        _sut.LowCutEnabled = true;
        Settle();

        _sut.ResetToFlatCommand.Execute().Subscribe();
        Settle();

        var saved = SavedSnapshot()[^1];

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

    public void Dispose() => _sut.Dispose();
}
