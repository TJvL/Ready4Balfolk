using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ManagedBass;
using Ready4Balfolk.Domain.Services.Logging;

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
    private readonly BehaviorSubject<bool> _isAvailable = new(true);
    private readonly ILoggerService _loggerService;

    private readonly CompositeDisposable _disposables = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private int _channel;
    private int _endSyncHandle;
    private int _preloadedChannel;
    private Uri? _preloadedUri;
    private bool _bassInitialized;
    private bool _bassFailed;
    private bool _disposed;

    public ManagedBassAudioPlaybackService(ILoggerService loggerService)
    {
        _loggerService = loggerService;

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
        _disposables.Add(_isAvailable);

        InitializeBass();
    }

    public bool IsPlaying => _channel != 0 && Bass.ChannelIsActive(_channel) == PlaybackState.Playing;
    public bool IsPaused => _channel != 0 && Bass.ChannelIsActive(_channel) == PlaybackState.Paused;
    public bool IsStopped => _channel == 0 || Bass.ChannelIsActive(_channel) == PlaybackState.Stopped;
    public bool AutoAdvance { get; set; } = true;

    public IObservable<Uri?> WhenSelectedChanged => _selectedChanged.AsObservable();
    public IObservable<Unit> WhenPlaybackStarted => _playbackStarted.AsObservable();
    public IObservable<Unit> WhenPlaybackPaused => _playbackPaused.AsObservable();
    public IObservable<Unit> WhenPlaybackRestarted => _playbackRestarted.AsObservable();
    public IObservable<Unit> WhenPlaybackCleared => _playbackCleared.AsObservable();
    public IObservable<Unit> WhenPlaybackEnded => _playbackEnded.AsObservable();
    public IObservable<TimeSpan> WhenProgressChanged { get; }
    public IObservable<TimeSpan> WhenDurationChanged => _durationChanged.AsObservable();
    public IObservable<bool> WhenAvailabilityChanged => _isAvailable.AsObservable();

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
                    _channel = Bass.CreateStream(path);

                    if (_channel == 0)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create stream for '{path}': {Bass.LastError}");
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

                    Bass.ChannelPlay(_channel);
                    _playbackStarted.OnNext(Unit.Default);
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
                    Bass.ChannelPlay(_channel, true);
                    _playbackRestarted.OnNext(Unit.Default);
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
                Bass.ChannelSetPosition(_channel, bytes);
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

    public Task PreloadNextAsync(Uri source)
    {
        return _bassFailed
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    FreePreloadedChannel();

                    var path = source.LocalPath;
                    _preloadedChannel = Bass.CreateStream(path);

                    if (_preloadedChannel == 0)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create preload stream for '{path}': {Bass.LastError}");
                    }

                    _preloadedUri = source;
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

    public Task NextAsync()
    {
        return Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                AdvanceToPreloaded();
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
        try
        {
            if (!Bass.Init())
            {
                _bassFailed = true;
                _isAvailable.OnNext(false);
                _ = _loggerService.CriticalAsync("Failed to initialize BASS audio",
                    new InvalidOperationException($"Bass.Init failed: {Bass.LastError}"));
                return;
            }
        }
        catch (Exception ex)
        {
            _bassFailed = true;
            _isAvailable.OnNext(false);
            _ = _loggerService.CriticalAsync("Failed to initialize BASS audio", ex);
            return;
        }

        _bassInitialized = true;
        _ = _loggerService.DebugAsync("BASS audio initialized");

        var flacPluginHandle = Bass.PluginLoad("bassflac");
        _ = flacPluginHandle == 0
            ? _loggerService.WarningAsync($"Failed to load BASSFLAC plugin: {Bass.LastError}")
            : _loggerService.DebugAsync("BASSFLAC plugin loaded");

        DiscoverSupportedExtensions(flacPluginHandle);
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
        _channel = 0;
    }

    private void FreePreloadedChannel()
    {
        if (_preloadedChannel == 0)
        {
            return;
        }

        Bass.StreamFree(_preloadedChannel);
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

    private void OnPlaybackEnded(int handle, int channel, int data, nint user)
    {
        _playbackEnded.OnNext(Unit.Default);

        if (AutoAdvance && _preloadedChannel != 0)
        {
            Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    AdvanceToPreloaded();
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }
    }

    private void AdvanceToPreloaded()
    {
        if (_preloadedChannel == 0)
        {
            return;
        }

        FreeChannel();

        _channel = _preloadedChannel;
        var uri = _preloadedUri;

        _preloadedChannel = 0;
        _preloadedUri = null;

        SetupEndSync();
        _selectedChanged.OnNext(uri);

        var lengthInBytes = Bass.ChannelGetLength(_channel);
        var lengthInSeconds = Bass.ChannelBytes2Seconds(_channel, lengthInBytes);
        _durationChanged.OnNext(TimeSpan.FromSeconds(lengthInSeconds));

        Bass.ChannelPlay(_channel);
        _playbackStarted.OnNext(Unit.Default);
    }

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
