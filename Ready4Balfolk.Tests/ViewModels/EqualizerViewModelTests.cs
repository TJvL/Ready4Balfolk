using System.Globalization;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Equalizer;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class EqualizerViewModelTests : IDisposable
{
    private readonly IAudioPlaybackService _audio = Substitute.For<IAudioPlaybackService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ITrackEqualizerService _trackEqualizers = Substitute.For<ITrackEqualizerService>();
    private readonly BehaviorSubject<Track?> _inForce = new(null);
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

        _trackEqualizers.WhenInForce.Returns(_inForce);
        _trackEqualizers.SaveAsync(Arg.Any<Track>(), Arg.Any<EqualizerSettings>()).Returns(Task.CompletedTask);

        _sut = new EqualizerViewModel(
            _audio, _settingsStore, _trackEqualizers, Substitute.For<ILoggerService>(),
            Substitute.For<INotificationService>(), _throttles.Scheduler);
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
            _audio, _settingsStore, _trackEqualizers, Substitute.For<ILoggerService>(),
            Substitute.For<INotificationService>(), _throttles.Scheduler);

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

    [Fact]
    public void ATrackPlayingThroughItsOwn_PutsItsCurveOnTheSlidersAndItsNameOnThePanel()
    {
        _sut.Enabled = true;

        _inForce.OnNext(OwnEqualizerTrack(preamp: -4, bass: 6));

        Assert.True(_sut.IsTrackEqualizer);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, UiStrings.Equalizer_TrackHeader, "Naragonia", "Salamandre"), _sut.Header);
        Assert.Equal(-4, _sut.PreampDecibels);
        Assert.Equal(6, _sut.Bands[0].Gain);
        // Putting it on the sliders is not a pull: nothing is heard again or written.
        _trackEqualizers.DidNotReceiveWithAnyArgs().Hear(default!, default!);
    }

    [Fact]
    public void APullWhileATrackPlaysThroughItsOwn_IsHeardAndKeptOnTheTrack()
    {
        _sut.Enabled = true;
        Settle();
        var savedBefore = SavedSnapshot().Count;
        var track = OwnEqualizerTrack(preamp: -4);
        _inForce.OnNext(track);

        _sut.Bands[3].Gain = -5;
        Settle();

        _trackEqualizers.Received().Hear(track, Arg.Is<EqualizerSettings>(curve => curve.BandGains[3] == -5 && curve.Enabled));
        _trackEqualizers.Received(1).SaveAsync(
            track, Arg.Is<EqualizerSettings>(curve => curve.BandGains[3] == -5 && curve.PreampDecibels == -4));
        // The global curve is untouched by a pull on a track's own.
        Assert.Equal(savedBefore, SavedSnapshot().Count);
        Assert.Equal(0, _stored.Equalizer.BandGains[3]);
    }

    [Fact]
    public void TheNextTrack_PutsTheGlobalCurveBack()
    {
        _sut.Enabled = true;
        _sut.PreampDecibels = -2;
        Settle();
        _inForce.OnNext(OwnEqualizerTrack(preamp: -9));

        _inForce.OnNext(null);

        Assert.False(_sut.IsTrackEqualizer);
        Assert.Equal(UiStrings.Equalizer_Title, _sut.Header);
        Assert.Equal(-2, _sut.PreampDecibels);
    }

    [Fact]
    public void TheSwitch_StaysTheGlobalOneWhileATrackIsShown()
    {
        // The one control that takes every effect out at once, whichever curve is on the sliders.
        _sut.Enabled = true;
        _inForce.OnNext(OwnEqualizerTrack(preamp: -9));

        _sut.Enabled = false;
        Settle();

        Assert.False(_stored.Equalizer.Enabled);
        Assert.Equal(0, _stored.Equalizer.PreampDecibels);
        _audio.Received().SetEqualizerAsync(Arg.Is<EqualizerSettings>(settings => !settings!.Enabled));
    }

    [Fact]
    public void BackToGlobal_SwitchesTheTracksOwnOffButKeepsItsCurve()
    {
        _sut.Enabled = true;
        var track = OwnEqualizerTrack(preamp: -4);
        _inForce.OnNext(track);

        _sut.BackToGlobalCommand.Execute().Subscribe();
        Settle();

        Assert.False(_sut.IsTrackEqualizer);
        Assert.Equal(0, _sut.PreampDecibels);
        _trackEqualizers.Received(1).SaveAsync(
            track, Arg.Is<EqualizerSettings>(curve => !curve.Enabled && curve.PreampDecibels == -4));
    }

    [Fact]
    public void ThePullComingBackThroughTheLibrary_DoesNotMoveTheSliders()
    {
        // Written on a throttle and published again a moment later. Taken as news, it would put the
        // sliders back to where the hand was when it was written rather than where it is now.
        _sut.Enabled = true;
        var track = OwnEqualizerTrack(preamp: -4);
        _inForce.OnNext(track);

        _sut.PreampDecibels = -6;
        Settle();
        _sut.PreampDecibels = -8;
        _inForce.OnNext(track with { Equalizer = track.Equalizer! with { PreampDecibels = -6 } });

        Assert.Equal(-8, _sut.PreampDecibels);
    }

    private static Track OwnEqualizerTrack(double preamp, double bass = 0) =>
        TestData.CreateTrack(artist: "Naragonia", title: "Salamandre") with
        {
            Equalizer = EqualizerSettings.Flat.WithBandGain(0, bass) with { Enabled = true, PreampDecibels = preamp }
        };

    private List<ApplicationSettings> SavedSnapshot()
    {
        lock (_saved)
        {
            return [.. _saved];
        }
    }

    public void Dispose()
    {
        _sut.Dispose();
        _inForce.Dispose();
    }
}
