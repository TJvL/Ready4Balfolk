using System.Reactive.Subjects;
using DynamicData;
using NSubstitute;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class TrackEqualizerServiceTests : IDisposable
{
    private static readonly EqualizerSettings Own = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -4 };

    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly IQueueConsumptionService _consumption = Substitute.For<IQueueConsumptionService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IAudioPlaybackService _audio = Substitute.For<IAudioPlaybackService>();
    private readonly Subject<IChangeSet<Track>> _libraryChanges = new();
    private readonly BehaviorSubject<IQueueItem?> _currentItem = new(null);
    private readonly BehaviorSubject<ApplicationSettings> _settings;
    private readonly List<IReadOnlyDictionary<string, EqualizerSettings>> _told = [];
    private readonly TrackEqualizerService _sut;

    private List<Track> _library = [];

    public TrackEqualizerServiceTests()
    {
        _settings = new BehaviorSubject<ApplicationSettings>(
            new ApplicationSettings() with { EqualizerOrNull = EqualizerSettings.Flat with { Enabled = true } });

        _trackStore.Connect().Returns(_libraryChanges);
        _trackStore.Current.Returns(_ => _library);
        _consumption.WhenCurrentItemChanged.Returns(_currentItem);
        _consumption.CurrentItem.Returns(_ => _currentItem.Value);
        _settingsStore.Observe().Returns(_settings);
        _settingsStore.Current.Returns(_ => _settings.Value);
        _audio.SetTrackEqualizersAsync(Arg.Any<IReadOnlyDictionary<string, EqualizerSettings>>())
            .Returns(call =>
            {
                _told.Add(call.Arg<IReadOnlyDictionary<string, EqualizerSettings>>()!);
                return Task.CompletedTask;
            });

        _sut = new TrackEqualizerService(
            _trackStore, _consumption, _settingsStore, _audio,
            Substitute.For<ILoggerService>(), Substitute.For<INotificationService>());
    }

    [Fact]
    public void TheEngine_IsToldEveryCurveInUse_AndNoneSwitchedOff()
    {
        // The whole set, so the track loaded ahead is shaped right before it plays.
        var withOwn = Track("Salamandre", Own);
        var switchedOff = Track("La Belle", Own with { Enabled = false });

        LibraryHolds(withOwn, switchedOff, Track("Plain", null));

        var told = Assert.Single(_told[^1]);
        Assert.Equal(withOwn.FileInfo.FullName, told.Key);
        Assert.Equal(Own, told.Value);
    }

    [Fact]
    public void APlayingTrackWithItsOwn_IsInForce()
    {
        var track = Track("Salamandre", Own);
        LibraryHolds(track);

        Plays(track);

        Assert.Equal(track, _sut.InForce);
    }

    [Fact]
    public void TheLibrarysCopy_IsTheOneInForce_NotTheQueuesSnapshot()
    {
        // Queued before it was given a curve: the queue's copy has none, the library's has one.
        var queued = Track("Salamandre", null);
        Plays(queued);

        LibraryHolds(queued with { Equalizer = Own });

        Assert.Equal(Own, _sut.InForce?.Equalizer);
    }

    [Fact]
    public void NothingIsInForce_WhileTheGlobalEqualizerIsOff()
    {
        var track = Track("Salamandre", Own);
        LibraryHolds(track);
        Plays(track);

        _settings.OnNext(_settings.Value with { EqualizerOrNull = EqualizerSettings.Flat with { Enabled = false } });

        Assert.Null(_sut.InForce);
    }

    [Fact]
    public void TheNextTrack_PutsTheGlobalOneBackInForce()
    {
        var track = Track("Salamandre", Own);
        LibraryHolds(track, Track("La Belle", null));
        Plays(track);

        Plays(_library[1]);

        Assert.Null(_sut.InForce);
    }

    [Fact]
    public void Hearing_TellsTheEngineAtOnceWithoutWritingAnything()
    {
        var track = Track("Salamandre", Own);
        LibraryHolds(track);
        var louder = Own with { PreampDecibels = 3 };

        _sut.Hear(track, louder);

        Assert.Equal(louder, _told[^1][track.FileInfo.FullName]);
        _trackStore.DidNotReceiveWithAnyArgs().SetEqualizerAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BackToGlobal_SwitchesTheTracksOwnOffAndKeepsItsCurve()
    {
        var track = Track("Salamandre", Own);

        await _sut.BackToGlobalAsync(track);

        await _trackStore.Received(1).SetEqualizerAsync(
            track.FileInfo.FullName, Own with { Enabled = false }, Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _sut.Dispose();
        _libraryChanges.Dispose();
        _currentItem.Dispose();
        _settings.Dispose();
    }

    private static Track Track(string title, EqualizerSettings? equalizer) =>
        TestData.CreateTrack(title: title) with { Equalizer = equalizer };

    private void LibraryHolds(params Track[] tracks)
    {
        _library = [.. tracks];
        _libraryChanges.OnNext(new ChangeSet<Track>([new Change<Track>(ListChangeReason.AddRange, tracks)]));
    }

    private void Plays(Track track) => _currentItem.OnNext(new TrackQueueItem(track, false));
}
