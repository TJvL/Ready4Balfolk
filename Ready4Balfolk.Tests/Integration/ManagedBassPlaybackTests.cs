using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using ManagedBass;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// The audio engine itself: what opening a file, moving through it and being handed something that
/// is not music actually do.
/// </summary>
/// <remarks>
/// Against the real library and the repository's own smoke-test audio, brought up on BASS's "no
/// sound" device, which is there so that a machine without a sound card can still run the library
/// exactly as it would against hardware. Nothing here needs a card, a display or a network.
///
/// A test that only proved a call did not throw would be worth nothing here: every assertion is
/// about something the DJ would see go wrong, which is why they are about the length that drives
/// the countdown, the announcement everything else waits on, the position a drag of the progress
/// bar lands on, what BASS says has been done to a stream, and what a deck does after it has been
/// handed a file it cannot play.
/// </remarks>
[Collection(ProcessWideAudioState.Name)]
public sealed class ManagedBassPlaybackTests : IDisposable
{
    /// <summary>How long the embedded smoke-test scale runs for.</summary>
    private static readonly TimeSpan ScaleLength = TimeSpan.FromSeconds(1.5);

    private readonly string _root;
    private readonly Uri _track;
    private readonly RecordingLoggerService _logger = new();
    private readonly ManagedBassAudioPlaybackService _sut;

    public ManagedBassPlaybackTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _track = Audio("scale.mp3");

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new ApplicationSettings());

        _sut = new ManagedBassAudioPlaybackService(_logger, settingsStore, useNoSoundDevice: true);
    }

    [Fact]
    public async Task Selecting_SaysWhatIsNowSelected()
    {
        Uri? selected = null;
        using (_sut.WhenSelectedChanged.Subscribe(value => selected = value))
        {
            await _sut.SelectAsync(_track);
        }

        Assert.Equal(_track, selected);
        Assert.True(_sut.IsStopped);
    }

    /// <summary>
    /// The length comes off the file rather than out of the tags, and everything that says when
    /// the evening will end is built on it.
    /// </summary>
    [Fact]
    public async Task Selecting_ReportsTheLengthTheFileActuallyIs()
    {
        var duration = TimeSpan.Zero;
        using (_sut.WhenDurationChanged.Subscribe(value => duration = value))
        {
            await _sut.SelectAsync(_track);
        }

        // Generous either way: this is about the length of the audio being read, not about
        // decoders agreeing to the millisecond.
        Assert.InRange(duration, ScaleLength - TimeSpan.FromMilliseconds(100), ScaleLength + TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Starting playback both starts it and announces it.
    /// </summary>
    /// <remarks>
    /// The announcement is what the rest of the evening runs on: the countdown, the screens and
    /// the phone all wait for it, so a start that worked and said nothing looks from every one of
    /// them like a dance that never began. It is only made when BASS says the audio is genuinely
    /// on its way, which is why it is asserted alongside the deck actually playing rather than
    /// instead of it.
    /// </remarks>
    [Fact]
    public async Task Playing_StartsTheTrackAndSaysThatItDid()
    {
        await _sut.SelectAsync(_track);

        var announcements = 0;
        using (_sut.WhenPlaybackStarted.Subscribe(_ => announcements++))
        {
            await _sut.PlayAsync();
        }

        Assert.Equal(1, announcements);
        Assert.True(_sut.IsPlaying);
    }

    /// <summary>
    /// An empty deck asked to play does nothing at all, and in particular does not decide the
    /// sound has gone.
    /// </summary>
    /// <remarks>
    /// The deck sits empty between clearing one dance and choosing the next, and play is a button
    /// a DJ presses there. Handing that press to BASS would have it refuse, which is the same
    /// answer it gives for an interface pulled out of the back of the machine: the DJ would be
    /// sent to look at a cable that is fine, and the screens would say the hall had lost its
    /// audio while nothing whatever was wrong.
    /// </remarks>
    [Fact]
    public async Task PlayingWithNothingSelected_DoesNotSayTheSoundHasGone()
    {
        var announcements = 0;
        var lost = false;

        using (_sut.WhenPlaybackStarted.Subscribe(_ => announcements++))
        using (_sut.WhenAvailabilityChanged.Subscribe(available => lost |= !available))
        {
            await _sut.PlayAsync();
        }

        Assert.Equal(0, announcements);
        Assert.False(lost);
        Assert.True(_sut.IsStopped);
    }

    /// <summary>
    /// A start BASS refuses is the device having gone, not this one track's problem, and is
    /// announced as the output being gone rather than left for the DJ to notice from a dance that
    /// never starts.
    /// </summary>
    /// <remarks>
    /// An interface unplugged mid-set is stood in for by freeing BASS itself out from under the
    /// service between the select and the play: the stream the select opened is still held, but
    /// there is nothing behind it any more to play anything on, which is exactly what
    /// <c>Bass.ChannelPlay</c> refuses. Playing again afterwards is what a DJ pressing the button
    /// a second time looks like, and the device still being gone must not turn into a second
    /// notice: the first one already said what needed saying.
    /// </remarks>
    [Fact]
    public async Task ARefusedStart_SaysTheOutputIsGoneRatherThanLeavingTheDeckSilentAboutWhy()
    {
        await _sut.SelectAsync(_track);

        Bass.Free();

        var lost = false;
        using (_sut.WhenAvailabilityChanged.Subscribe(available => lost |= !available))
        {
            await _sut.PlayAsync();

            var reported = await _logger.NextErrorAsync(TestContext.Current.CancellationToken);
            Assert.Equal(DomainStrings.Audio_OutputGone, reported.Message);

            // Asked again, which is what a DJ pressing play a second time looks like: the device
            // is still gone, but the notice already said so once.
            await _sut.PlayAsync();
        }

        Assert.True(lost);
        Assert.Single(_logger.Errors);

        // Left usable for whatever runs after this test, rather than freed for the rest of the
        // process: the no-sound device comes back up the same way it did the first time.
        Assert.True(Bass.Init(0), $"BASS did not come back up after being freed: {Bass.LastError}");
    }

    /// <summary>
    /// A drag of the progress bar lands where it was dropped, and playback carries on from there.
    /// </summary>
    /// <remarks>
    /// The seek stops the channel to throw away what was already on its way to the speakers and
    /// starts it again afterwards. Starting again at the beginning is the failure this is here for:
    /// it looks like nothing happened, in front of a floor waiting for the tune to move on.
    /// </remarks>
    [Fact]
    public async Task Seeking_MovesThePlayheadAndKeepsPlayingFromThere()
    {
        // Several minutes of dance in one file is the ordinary case, and a longer one is what
        // leaves room to see where the playhead landed before the track runs out.
        var longer = LongAudio("long.mp3");
        await _sut.SelectAsync(longer);
        await _sut.PlayAsync();

        await _sut.SeekAsync(TimeSpan.FromSeconds(3));

        Assert.True(_sut.IsPlaying);

        // Cut off rather than waited on forever: nobody is at the keyboard, and the progress
        // stream only reports while something is playing.
        var reported = await _sut.WhenProgressChanged
            .Timeout(TimeSpan.FromSeconds(5))
            .FirstAsync()
            .ToTask(TestContext.Current.CancellationToken);

        // Where the seek put it, plus however long the report took to arrive. Starting again from
        // the beginning, which is what a seek that only threw the buffer away would do, is a
        // reading near zero.
        Assert.InRange(reported, TimeSpan.FromSeconds(2.9), TimeSpan.FromSeconds(4.5));
    }

    [Fact]
    public async Task ATrackNothingCanPlay_FailsSayingWhichFileItWas()
    {
        var notAudio = await NotAudio("broken.mp3");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SelectAsync(notAudio));

        Assert.Contains("broken.mp3", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// One unplayable file is one dance lost, not the evening: the deck is still there afterwards.
    /// </summary>
    /// <remarks>
    /// The lock around a select is released whatever happens, so the next dance can still be put
    /// on. Held, it would take every later select with it, and the application would look alive
    /// while nothing the DJ pressed did anything at all.
    /// </remarks>
    [Fact]
    public async Task ATrackNothingCanPlay_LeavesTheDeckUsable()
    {
        var notAudio = await NotAudio("broken.mp3");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SelectAsync(notAudio));

        await _sut.SelectAsync(_track);
        await _sut.PlayAsync();

        Assert.True(_sut.IsPlaying);
    }

    /// <summary>Same again for the track loaded ahead, which fails an evening earlier.</summary>
    [Fact]
    public async Task ATrackNothingCanPlay_LoadedAhead_LeavesTheDeckUsable()
    {
        var notAudio = await NotAudio("broken.mp3");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.PreloadNextAsync(notAudio));

        await _sut.SelectAsync(_track);
        await _sut.PlayAsync();

        Assert.True(_sut.IsPlaying);
    }

    /// <summary>
    /// Every stream gets its effect chain, the one loaded ahead included, and keeps it when it
    /// becomes the one playing. Without that, every second dance plays flat.
    /// </summary>
    /// <remarks>
    /// What is measured is the preamp, because it is the one part of the chain BASS will report
    /// back: applying the settings sets the channel's own volume trim, so a stream carrying that
    /// trim is a stream the chain reached, and a stream with no chain is left at 1. The gains
    /// themselves are not readable off a channel, and are measured in the audio instead by the
    /// tests over the chain.
    ///
    /// The handover matters as much as the attaching: selecting the file that was loaded ahead
    /// takes the existing handle over rather than opening the file again, so this asks the same
    /// question of the same stream before and after it becomes the one the room hears.
    /// </remarks>
    [Fact]
    public async Task EveryStreamGetsItsEffectChain()
    {
        Assert.True(_sut.IsEqualizerAvailable);

        var next = Audio("next.mp3");
        await _sut.SelectAsync(_track);
        await _sut.PreloadNextAsync(next);
        await _sut.SetEqualizerAsync(new EqualizerSettings { Enabled = true, PreampDecibels = -6 });

        var (playing, preloaded) = _sut.OpenChannels;
        Assert.NotEqual(0, playing);
        Assert.NotEqual(0, preloaded);

        // Half the amplitude, which is what -6 dB is as the trim BASS holds it in.
        var trim = Math.Pow(10, -6 / 20.0);

        Bass.ChannelGetAttribute(playing, ChannelAttribute.Volume, out var onThePlayingStream);
        Bass.ChannelGetAttribute(preloaded, ChannelAttribute.Volume, out var onTheStreamLoadedAhead);

        Assert.Equal(trim, onThePlayingStream, 0.001);
        Assert.Equal(trim, onTheStreamLoadedAhead, 0.001);

        await _sut.SelectAsync(next);

        var (nowPlaying, _) = _sut.OpenChannels;
        Assert.Equal(preloaded, nowPlaying);

        Bass.ChannelGetAttribute(nowPlaying, ChannelAttribute.Volume, out var afterTheHandover);
        Assert.Equal(trim, afterTheHandover, 0.001);
    }

    /// <summary>The embedded smoke-test audio, written into the temporary tree.</summary>
    private Uri Audio(string name)
    {
        var path = Path.Combine(_root, name);

        using (var source = typeof(ManagedBassPlaybackTests).Assembly
                                .GetManifestResourceStream("scale.mp3")
                            ?? throw new InvalidOperationException("Embedded audio 'scale.mp3' is missing."))
        using (var destination = File.Create(path))
        {
            source.CopyTo(destination);
        }

        return new Uri(path);
    }

    /// <summary>The same scale several times over, for a track long enough to move around in.</summary>
    /// <remarks>
    /// One mp3 after another is a valid mp3: the format is a run of frames, which is what makes it
    /// something a stream can be started in the middle of in the first place.
    /// </remarks>
    private Uri LongAudio(string name)
    {
        var path = Path.Combine(_root, name);

        using (var source = typeof(ManagedBassPlaybackTests).Assembly
                                .GetManifestResourceStream("scale.mp3")
                            ?? throw new InvalidOperationException("Embedded audio 'scale.mp3' is missing."))
        using (var destination = File.Create(path))
        {
            for (var copy = 0; copy < 8; copy++)
            {
                source.Position = 0;
                source.CopyTo(destination);
            }
        }

        return new Uri(path);
    }

    /// <summary>A file named like music and containing nothing of the sort.</summary>
    /// <remarks>
    /// What a library holds in practice: a download that stopped halfway, a text file somebody
    /// renamed. The extension is what the library scan goes on, so this is a file the queue can
    /// genuinely arrive at.
    /// </remarks>
    private async Task<Uri> NotAudio(string name)
    {
        var path = Path.Combine(_root, name);
        await File.WriteAllTextAsync(path, "not music", TestContext.Current.CancellationToken);
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
