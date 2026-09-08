using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// What preloading is worth: the dance that comes next starts from the stream that was opened
/// minutes ago rather than from the disk.
/// </summary>
/// <remarks>
/// Against the real library and real files, because the whole subject is what BASS is holding: a
/// substituted audio service would only prove that this test can call its own mock. Both files are
/// the repository's own smoke-test audio, and BASS is brought up against its "no sound" device, so
/// nothing here needs a sound card.
///
/// Which stream a select used is read out of the debug trace, since a BASS handle is not a thing
/// this service hands anybody. Those lines are written at debug level, so a normal run does not
/// carry them: they are how a stream's life is followed here and while debugging, not something a
/// DJ's exported log will show.
/// </remarks>
[Collection(ProcessWideAudioState.Name)]
public sealed class ManagedBassAudioPlaybackServiceTests : IDisposable
{
    private readonly string _root;
    private readonly Uri _first;
    private readonly Uri _second;
    private readonly RecordingLoggerService _logger = new();
    private readonly ManagedBassAudioPlaybackService _sut;

    public ManagedBassAudioPlaybackServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _first = Audio("first.mp3");
        _second = Audio("second.mp3");

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new ApplicationSettings());

        _sut = new ManagedBassAudioPlaybackService(_logger, settingsStore, useNoSoundDevice: true);
    }

    [Fact]
    public async Task SelectingThePreloadedTrack_PlaysTheStreamThatIsAlreadyOpen()
    {
        await _sut.PreloadNextAsync(_first);

        await _sut.SelectAsync(_first);

        Assert.Contains(Preloaded("first.mp3"), _logger.Debug);
        Assert.DoesNotContain(Opened("first.mp3"), _logger.Debug);
    }

    /// <summary>An adopted stream is a stream: it plays, it is not merely a handle held on to.</summary>
    [Fact]
    public async Task ThePreloadedStream_IsTheOneThatThenPlays()
    {
        await _sut.PreloadNextAsync(_first);
        await _sut.SelectAsync(_first);

        await _sut.PlayAsync();

        Assert.True(_sut.IsPlaying);
        Assert.Contains(Preloaded("first.mp3"), _logger.Debug);
        Assert.DoesNotContain(Opened("first.mp3"), _logger.Debug);
    }

    /// <summary>
    /// The length is read off the stream that is now playing, so a screen showing a dance running
    /// says the same thing whether that stream was opened minutes ago or a moment ago.
    /// </summary>
    [Fact]
    public async Task ThePreloadedStream_StillKnowsHowLongTheTrackIs()
    {
        await _sut.PreloadNextAsync(_first);

        var duration = TimeSpan.Zero;
        using (_sut.WhenDurationChanged.Subscribe(value => duration = value))
        {
            await _sut.SelectAsync(_first);
        }

        Assert.True(duration > TimeSpan.Zero);
        Assert.Contains(Preloaded("first.mp3"), _logger.Debug);
        Assert.DoesNotContain(Opened("first.mp3"), _logger.Debug);
    }

    /// <summary>A preloaded stream is one track's, and playing another one is not it.</summary>
    [Fact]
    public async Task SelectingSomethingElse_OpensItAndKeepsThePreloadedTrackWaiting()
    {
        await _sut.PreloadNextAsync(_first);

        await _sut.SelectAsync(_second);
        await _sut.SelectAsync(_first);

        Assert.Contains(Opened("second.mp3"), _logger.Debug);

        // Still there afterwards: the dance it was opened for is normally the one coming, and a
        // DJ reaching past it once is no reason to throw the head start away.
        Assert.Contains(Preloaded("first.mp3"), _logger.Debug);
    }

    /// <summary>
    /// Taken once and only once. A handle that stayed in the slot after being adopted would be
    /// freed under the track it is playing the next time anything preloads.
    /// </summary>
    [Fact]
    public async Task ThePreloadedStream_IsGoodForOneSelect()
    {
        await _sut.PreloadNextAsync(_first);
        await _sut.SelectAsync(_first);

        await _sut.SelectAsync(_first);

        Assert.Contains(Opened("first.mp3"), _logger.Debug);
    }

    /// <summary>
    /// The track after this one being loaded must not reach into the track playing. It does the
    /// moment the same handle is both the playing channel and the one still sitting in the slot.
    /// </summary>
    [Fact]
    public async Task LoadingTheTrackAfterNext_DoesNotStopTheOneThatWasPreloaded()
    {
        await _sut.PreloadNextAsync(_first);
        await _sut.SelectAsync(_first);
        await _sut.PlayAsync();

        await _sut.PreloadNextAsync(_second);

        Assert.True(_sut.IsPlaying);
    }

    [Fact]
    public async Task ClearingThePreload_LetsItGo()
    {
        await _sut.PreloadNextAsync(_first);
        await _sut.ClearPreloadAsync();

        await _sut.SelectAsync(_first);

        Assert.Contains(Opened("first.mp3"), _logger.Debug);
        Assert.DoesNotContain(Preloaded("first.mp3"), _logger.Debug);
    }

    /// <summary>Clearing is the end of everything loaded, the track waiting included.</summary>
    [Fact]
    public async Task Clearing_LetsThePreloadGoToo()
    {
        await _sut.PreloadNextAsync(_first);
        await _sut.ClearAsync();

        await _sut.SelectAsync(_first);

        Assert.Contains(Opened("first.mp3"), _logger.Debug);
    }

    /// <summary>
    /// What the gap between two dances needs: the one that just finished goes, and the one it is
    /// waiting for stays open. Letting everything go here is the head start thrown away.
    /// </summary>
    [Fact]
    public async Task ClearingWhatIsPlaying_LeavesTheTrackLoadedAheadWaiting()
    {
        await _sut.SelectAsync(_first);
        await _sut.PlayAsync();
        await _sut.PreloadNextAsync(_second);

        await _sut.ClearPlayingAsync();

        Assert.True(_sut.IsStopped);

        await _sut.SelectAsync(_second);

        Assert.Contains(Preloaded("second.mp3"), _logger.Debug);
        Assert.DoesNotContain(Opened("second.mp3"), _logger.Debug);
    }

    /// <summary>
    /// Asking again for the dance that is already waiting is what the start of a gap does, since
    /// the queue can have moved while the last one played. Opening that file a second time is the
    /// head start paid for twice.
    /// </summary>
    [Fact]
    public async Task PreloadingTheTrackThatIsAlreadyWaiting_KeepsTheStreamItHas()
    {
        await _sut.PreloadNextAsync(_first);
        await _sut.PreloadNextAsync(_first);

        await _sut.SelectAsync(_first);

        Assert.Single(_logger.Debug, line => line == LoadedAhead("first.mp3"));
        Assert.Contains(Preloaded("first.mp3"), _logger.Debug);
    }

    /// <summary>
    /// Two names differing only in case are two files where the music lives, so a stream opened for
    /// one of them is never the other's. Getting this wrong costs nothing when it is too strict and
    /// puts the wrong dance through the hall when it is too loose.
    /// </summary>
    [Fact]
    public async Task ATrackNamedLikeThePreloadedOneButInAnotherCase_IsOpenedOnItsOwn()
    {
        // Written by the same helper, so on a filesystem that reads the two names as one file this
        // is that one file under both spellings, and the select below still has something to open.
        var upper = Audio("Bourree.mp3");
        var lower = Audio("bourree.mp3");

        await _sut.PreloadNextAsync(upper);
        await _sut.SelectAsync(lower);

        Assert.Contains(Opened("bourree.mp3"), _logger.Debug);
        Assert.DoesNotContain(Preloaded("bourree.mp3"), _logger.Debug);
    }

    [Fact]
    public async Task ATrackThatWasNeverPreloaded_IsOpenedWhenItIsSelected()
    {
        await _sut.SelectAsync(_first);

        Assert.Contains(Opened("first.mp3"), _logger.Debug);
    }

    private static string Preloaded(string name) => $"Playing the stream preloaded for '{name}'";

    private static string Opened(string name) => $"Opened a stream for '{name}'";

    private static string LoadedAhead(string name) => $"Loaded '{name}' ahead";

    /// <summary>The embedded smoke-test audio, written into the temporary tree.</summary>
    private Uri Audio(string name)
    {
        var path = Path.Combine(_root, name);

        using (var source = typeof(ManagedBassAudioPlaybackServiceTests).Assembly
                                .GetManifestResourceStream("scale.mp3")
                            ?? throw new InvalidOperationException("Embedded audio 'scale.mp3' is missing."))
        using (var destination = File.Create(path))
        {
            source.CopyTo(destination);
        }

        return new Uri(path);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _logger.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
