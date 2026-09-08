using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ManagedBass;
using ManagedBass.Fx;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Domain.Services.Audio;

public sealed class ManagedBassAudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly Subject<Uri?> _selectedChanged = new();
    private readonly Subject<Unit> _playbackStarted = new();
    private readonly Subject<Unit> _playbackPaused = new();
    private readonly Subject<Unit> _playbackRestarted = new();
    private readonly Subject<Unit> _playbackCleared = new();
    private readonly Subject<Unit> _playbackEnded = new();
    private readonly Subject<TimeSpan> _durationChanged = new();
    private readonly AudioAvailability _availability;
    private readonly ILoggerService _loggerService;
    private readonly bool _useNoSoundDevice;

    private readonly CompositeDisposable _disposables = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly Dictionary<int, EqualizerChain> _equalizerChains = [];

    private int _channel;
    private int _endSyncHandle;
    private int _preloadedChannel;
    private Uri? _preloadedUri;
    private bool _bassInitialized;
    private bool _bassFailed;
    private bool _disposed;
    private EqualizerSettings _equalizerSettings = EqualizerSettings.Flat;

    /// <param name="loggerService">Where initialisation results and playback failures are recorded.</param>
    /// <param name="settingsStore">Supplies the equalizer settings the effect chain starts from.</param>
    /// <param name="useNoSoundDevice">
    /// Initialises BASS against its "no sound" device instead of the default output. The library,
    /// its plugins and the whole effect chain come up exactly as they would against real hardware;
    /// only the audio goes nowhere. For the CI smoke test, where the runner has no sound card at
    /// all, this keeps the check measuring what it is there to measure, that the native libraries
    /// shipped and load, rather than whether the machine can make a noise.
    /// </param>
    public ManagedBassAudioPlaybackService(
        ILoggerService loggerService,
        ISettingsStore settingsStore,
        bool useNoSoundDevice = false)
    {
        _loggerService = loggerService;
        _availability = new AudioAvailability(loggerService);
        _useNoSoundDevice = useNoSoundDevice;
        _equalizerSettings = settingsStore.Current.Equalizer;

        WhenProgressChanged = Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Where(_ => IsPlaying)
            .Select(_ => GetPosition())
            .DistinctUntilChanged(t => (int)t.TotalMilliseconds);

        _disposables.Add(_selectedChanged);
        _disposables.Add(_playbackStarted);
        _disposables.Add(_playbackPaused);
        _disposables.Add(_playbackRestarted);
        _disposables.Add(_playbackCleared);
        _disposables.Add(_playbackEnded);
        _disposables.Add(_durationChanged);
        _disposables.Add(_availability);

        InitializeBass();
    }

    public bool IsPlaying => _channel != 0 && Bass.ChannelIsActive(_channel) == PlaybackState.Playing;
    public bool IsPaused => _channel != 0 && Bass.ChannelIsActive(_channel) == PlaybackState.Paused;
    public bool IsStopped => _channel == 0 || Bass.ChannelIsActive(_channel) == PlaybackState.Stopped;
    public bool IsEqualizerAvailable { get; private set; }

    /// <summary>
    /// The BASS handles of the streams this service currently has open: the one a select landed
    /// on, and the one loaded ahead. Zero where there is none.
    /// </summary>
    /// <remarks>
    /// Only a test reads these, so that it can ask BASS itself what has been done to a stream this
    /// class opened. Nothing else needs them: a handle is meaningless outside the library that
    /// issued it, and everything the rest of the application wants from a channel is already an
    /// observable or a property above.
    /// </remarks>
    internal (int Playing, int Preloaded) OpenChannels => (_channel, _preloadedChannel);

    public IObservable<Uri?> WhenSelectedChanged => _selectedChanged.AsObservable();
    public IObservable<Unit> WhenPlaybackStarted => _playbackStarted.AsObservable();
    public IObservable<Unit> WhenPlaybackPaused => _playbackPaused.AsObservable();
    public IObservable<Unit> WhenPlaybackRestarted => _playbackRestarted.AsObservable();
    public IObservable<Unit> WhenPlaybackCleared => _playbackCleared.AsObservable();
    public IObservable<Unit> WhenPlaybackEnded => _playbackEnded.AsObservable();
    public IObservable<TimeSpan> WhenProgressChanged { get; }
    public IObservable<TimeSpan> WhenDurationChanged => _durationChanged.AsObservable();
    public IObservable<bool> WhenAvailabilityChanged => _availability.WhenChanged;

    public Task SelectAsync(Uri source)
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    FreeChannel();

                    var path = source.LocalPath;
                    var preloaded = TakePreloadedChannel(source);

                    if (preloaded != 0)
                    {
                        // Already open, already through its effect chain, and nothing left to read
                        // from the disk before the first note. This is what the gap between two
                        // dances is for.
                        _channel = preloaded;
                        _ = _loggerService.DebugAsync(
                            $"Playing the stream preloaded for '{LogPaths.Name(path)}'");
                    }
                    else
                    {
                        _channel = Bass.CreateStream(path);

                        if (_channel == 0)
                        {
                            throw new InvalidOperationException(
                                $"Failed to create stream for '{LogPaths.Name(path)}': {Bass.LastError}");
                        }

                        AttachEqualizer(_channel);
                        _ = _loggerService.DebugAsync($"Opened a stream for '{LogPaths.Name(path)}'");
                    }

                    SetupEndSync();
                    _selectedChanged.OnNext(source);

                    var lengthInBytes = Bass.ChannelGetLength(_channel);
                    var lengthInSeconds = Bass.ChannelBytes2Seconds(_channel, lengthInBytes);
                    _durationChanged.OnNext(TimeSpan.FromSeconds(lengthInSeconds));
                }
                finally
                {
                    _semaphore.Release();
                }
            });
    }

    public Task PlayAsync()
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (_channel == 0)
                    {
                        return;
                    }

                    if (StartChannel())
                    {
                        _playbackStarted.OnNext(Unit.Default);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            });
    }

    public Task PauseAsync()
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (_channel == 0)
                    {
                        return;
                    }

                    Bass.ChannelPause(_channel);
                    _playbackPaused.OnNext(Unit.Default);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
    }

    public Task RestartAsync()
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (_channel == 0)
                    {
                        return;
                    }

                    Bass.ChannelSetPosition(_channel, 0);

                    if (StartChannel(true))
                    {
                        _playbackRestarted.OnNext(Unit.Default);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            });
    }

    public Task SeekAsync(TimeSpan position)
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_channel == 0)
                {
                    return;
                }

                var bytes = Bass.ChannelSeconds2Bytes(_channel, position.TotalSeconds);
                var wasPlaying = Bass.ChannelIsActive(_channel) == PlaybackState.Playing;

                // Stopped, moved, and started again, rather than moved underneath a running
                // channel. What has already been handed to the speakers is not part of the track
                // any more: pausing keeps it and plays it out after the jump, on top of the new
                // position, so the room hears the same tune twice a moment apart. Stopping is what
                // throws that buffer away; the position survives it, and playing again picks up
                // where the seek put it rather than at the start.
                if (wasPlaying)
                {
                    Bass.ChannelStop(_channel);
                }

                Bass.ChannelSetPosition(_channel, bytes);

                if (wasPlaying)
                {
                    StartChannel();
                }

                _ = _loggerService.DebugAsync(
                    $"Seeked to {position} on channel {_channel}, error {Bass.LastError}");
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    public Task ClearAsync()
    {
        return Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                FreeChannel();
                FreePreloadedChannel();
                _selectedChanged.OnNext(null);
                _playbackCleared.OnNext(Unit.Default);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    public Task ClearPlayingAsync()
    {
        return Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                FreeChannel();
                _selectedChanged.OnNext(null);
                _playbackCleared.OnNext(Unit.Default);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    public Task PreloadNextAsync(Uri source)
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (IsAlreadyPreloaded(source))
                    {
                        // The same file, already open and waiting. Freeing it to open it again is
                        // the head start thrown away and then paid for a second time.
                        return;
                    }

                    FreePreloadedChannel();

                    var path = source.LocalPath;
                    _preloadedChannel = Bass.CreateStream(path);

                    if (_preloadedChannel == 0)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create preload stream for '{LogPaths.Name(path)}': {Bass.LastError}");
                    }

                    // The preloaded stream needs the chain too. Without this every second track
                    // plays flat, because selecting it only takes the handle over.
                    AttachEqualizer(_preloadedChannel);

                    _preloadedUri = source;
                    _ = _loggerService.DebugAsync($"Loaded '{LogPaths.Name(path)}' ahead");
                }
                finally
                {
                    _semaphore.Release();
                }
            });
    }

    public Task ClearPreloadAsync()
    {
        return Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                FreePreloadedChannel();
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    public Task SetEqualizerAsync(EqualizerSettings equalizerSettings)
    {
        return _bassFailed || !IsEqualizerAvailable
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    _equalizerSettings = equalizerSettings;

                    ApplyEqualizer(_channel);
                    ApplyEqualizer(_preloadedChannel);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
    }

    ~ManagedBassAudioPlaybackService()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _disposables.Dispose();
            _semaphore.Dispose();
        }

        FreeChannel();
        FreePreloadedChannel();

        if (_bassInitialized)
        {
            Bass.Free();
        }
    }

    private void InitializeBass()
    {
        // -1 is the default output; 0 is BASS's "no sound" device.
        var device = _useNoSoundDevice ? 0 : -1;

        try
        {
            if (!Bass.Init(device))
            {
                _bassFailed = true;
                _availability.NeverCameUp(
                    new InvalidOperationException($"Bass.Init failed: {Bass.LastError}"));
                return;
            }
        }
        catch (Exception ex)
        {
            _bassFailed = true;
            _availability.NeverCameUp(ex);
            return;
        }

        _bassInitialized = true;
        _ = _loggerService.DebugAsync("BASS audio initialized");

        // Runs the whole effect chain in floating point even for 16 bit files, so nothing is
        // quantised between filters. Must be set before any effect exists.
        Bass.FloatingPointDSP = true;

        InitializeEqualizer();

        var flacPluginHandle = Bass.PluginLoad(ResolveNativeLibrary(
            OperatingSystem.IsWindows() ? "bassflac.dll" : "libbassflac.so"));
        _ = flacPluginHandle == 0
            ? _loggerService.WarningAsync($"Failed to load BASSFLAC plugin: {Bass.LastError}")
            : _loggerService.DebugAsync("BASSFLAC plugin loaded");

        DiscoverSupportedExtensions(flacPluginHandle);
    }

    /// <summary>
    /// Finds a BASS add-on on disk so it can be loaded by full path.
    /// </summary>
    /// <remarks>
    /// PluginLoad is a LoadLibrary/dlopen from inside BASS itself, so it searches the operating
    /// system's library path and knows nothing about where .NET put the file. Under
    /// PublishSingleFile the natives are extracted to a temp directory that is on neither path,
    /// and the Windows builds shipped without FLAC support because of it. Managed P/Invokes such
    /// as bass and bass_fx are unaffected, because those go through .NET's own resolver, which
    /// is also where the extraction directory can be read back from.
    /// </remarks>
    private static string ResolveNativeLibrary(string fileName)
    {
        var directories = new List<string> { AppContext.BaseDirectory };

        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string searchPath)
        {
            directories.AddRange(searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var candidate in directories.Select(directory => Path.Combine(directory, fileName)))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Nothing found. Hand back the bare name so BASS searches the system path and reports its
        // own error, rather than inventing a path that is certain to fail.
        return fileName;
    }

    /// <summary>
    /// BASS_FX is a library rather than a BASS plugin, so it is never loaded by PluginLoad. Reading
    /// its version forces the native load, without which every ChannelSetFX for an add-on effect
    /// type fails with BASS_ERROR_ILLTYPE. A missing add-on costs the equalizer, not playback.
    /// </summary>
    private void InitializeEqualizer()
    {
        try
        {
            var version = BassFx.Version;
            IsEqualizerAvailable = true;
            _ = _loggerService.DebugAsync($"BASS_FX loaded: {version}");
        }
        catch (DllNotFoundException ex)
        {
            IsEqualizerAvailable = false;
            _ = _loggerService.WarningAsync($"BASS_FX unavailable, equalizer disabled: {ex.Message}");
        }
    }

    private void DiscoverSupportedExtensions(int flacPluginHandle)
    {
        // Built-in BASS formats (not queryable on BASS)
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3",
            ".mp2",
            ".mp1",
            ".wav",
            ".aif",
            ".aiff",
            ".ogg"
        };

        if (flacPluginHandle != 0)
        {
            CollectPluginExtensions(Bass.PluginGetInfo(flacPluginHandle), extensions);
        }

        SupportedAudioFormats.Initialize(extensions);
        _ = _loggerService.InfoAsync(
            $"Supported audio extensions: {string.Join(", ", extensions.Order())}");
    }

    private static void CollectPluginExtensions(PluginInfo info, HashSet<string> extensions)
    {
        foreach (var format in info.Formats)
        {
            foreach (var part in format.FileExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var dotIndex = part.IndexOf('.');
                if (dotIndex >= 0)
                {
                    extensions.Add(part[dotIndex..]);
                }
            }
        }
    }

    /// <summary>
    /// Builds the effect chain on a freshly created stream and applies the current settings. The
    /// effects are freed along with the stream, so only the lookup needs cleaning up afterwards.
    /// </summary>
    private void AttachEqualizer(int channel)
    {
        if (!IsEqualizerAvailable || channel == 0)
        {
            return;
        }

        var chain = EqualizerChain.TryCreate(channel);

        if (chain == null)
        {
            _ = _loggerService.WarningAsync($"Failed to attach equalizer: {Bass.LastError}");
            return;
        }

        _equalizerChains[channel] = chain;
        chain.Apply(_equalizerSettings);
    }

    private void ApplyEqualizer(int channel)
    {
        if (channel != 0 && _equalizerChains.TryGetValue(channel, out var chain))
        {
            chain.Apply(_equalizerSettings);
        }
    }

    /// <summary>Starts the current channel, and says whether sound is actually on its way.</summary>
    /// <remarks>
    /// A refused start is not this one track's problem: BASS says no when the device it was
    /// initialised against has gone, which is what an interface unplugged mid-set looks like.
    /// Announcing playback anyway left the desktop, the screens and the phone all showing a dance
    /// while the hall was silent, and no end of track ever arrived to move the evening on.
    /// </remarks>
    private bool StartChannel(bool restart = false)
    {
        if (!Bass.ChannelPlay(_channel, restart))
        {
            _availability.Gone($"Bass.ChannelPlay failed: {Bass.LastError}");
            return false;
        }

        _availability.Working();
        return true;
    }

    private void FreeChannel()
    {
        if (_channel == 0)
        {
            return;
        }

        if (_endSyncHandle != 0)
        {
            Bass.ChannelRemoveSync(_channel, _endSyncHandle);
            _endSyncHandle = 0;
        }

        Bass.ChannelStop(_channel);
        Bass.StreamFree(_channel);
        _equalizerChains.Remove(_channel);
        _channel = 0;
    }

    private void FreePreloadedChannel()
    {
        if (_preloadedChannel == 0)
        {
            return;
        }

        Bass.StreamFree(_preloadedChannel);
        _equalizerChains.Remove(_preloadedChannel);
        _preloadedChannel = 0;
        _preloadedUri = null;
    }

    private void SetupEndSync()
    {
        if (_channel == 0)
        {
            return;
        }

        _endSyncHandle = Bass.ChannelSetSync(_channel, SyncFlags.End, 0, OnPlaybackEnded);
    }

    private void OnPlaybackEnded(int handle, int channel, int data, nint user) =>
        _playbackEnded.OnNext(Unit.Default);

    /// <summary>The stream already open for <paramref name="source" />, or nothing.</summary>
    /// <remarks>
    /// Taking it hands the handle over: the slot is empty afterwards, so whoever took it is the
    /// only thing left that can free it. A stream nobody takes stays in the slot and is freed by a
    /// preload of some other file, by clearing, or at disposal, because the dance it was opened for
    /// is usually still the one coming.
    ///
    /// The paths are compared exactly rather than as URIs, which match without regard to case: two
    /// files whose names differ only in case are two files on Linux, and a mismatch here costs one
    /// stream open where a wrong match would put the wrong music through the hall.
    /// </remarks>
    private int TakePreloadedChannel(Uri source)
    {
        if (!IsAlreadyPreloaded(source))
        {
            return 0;
        }

        var channel = _preloadedChannel;
        _preloadedChannel = 0;
        _preloadedUri = null;
        return channel;
    }

    /// <summary>Whether the stream waiting in the slot is this exact file's.</summary>
    private bool IsAlreadyPreloaded(Uri source) =>
        _preloadedChannel != 0 &&
        string.Equals(_preloadedUri?.LocalPath, source.LocalPath, StringComparison.Ordinal);

    private TimeSpan GetPosition()
    {
        if (_channel == 0)
        {
            return TimeSpan.Zero;
        }

        var posBytes = Bass.ChannelGetPosition(_channel);
        var posSecs = Bass.ChannelBytes2Seconds(_channel, posBytes);
        return TimeSpan.FromSeconds(posSecs);
    }
}
