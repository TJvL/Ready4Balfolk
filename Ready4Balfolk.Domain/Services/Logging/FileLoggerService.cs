using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ready4Balfolk.Domain.Services.Logging;

public sealed class FileLoggerService : ILoggerService, IDisposable
{
    private const string LogFileName = "app.log";
    private const string PreviousLogFileName = "app.log.1";
    private const long MaxFileSizeBytes = 512 * 1024;

    private readonly IFileInfo _logFile;
    private readonly IFileInfo _previousLogFile;
    private readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ReplaySubject<LogEntry> _errorSubject = new(bufferSize: 10);

    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public IObservable<LogEntry> WhenErrorLogged => _errorSubject.AsObservable();

    public FileLoggerService(IDirectoryInfo logDirectory)
    {
        logDirectory.Create();
        _logFile = logDirectory.FileSystem.FileInfo.New(Path.Combine(logDirectory.FullName, LogFileName));
        _previousLogFile =
            logDirectory.FileSystem.FileInfo.New(Path.Combine(logDirectory.FullName, PreviousLogFileName));
    }

    public Task LogAsync(LogLevel logLevel, string message)
    {
        if (logLevel < MinimumLevel)
        {
            return Task.CompletedTask;
        }

        var line = FormatLine(logLevel, message);
        return Task.Run(() => WriteLineAsync(line));
    }

    public Task DebugAsync(string message) => LogAsync(LogLevel.Debug, message);

    public Task InfoAsync(string message) => LogAsync(LogLevel.Info, message);

    public Task WarningAsync(string message) => LogAsync(LogLevel.Warning, message);

    public Task ErrorAsync(string message)
    {
        _errorSubject.OnNext(new LogEntry(LogLevel.Error, message));
        return LogAsync(LogLevel.Error, message);
    }

    public Task ErrorAsync(string message, Exception exception)
    {
        _errorSubject.OnNext(new LogEntry(LogLevel.Error, message, exception));
        return LogAsync(LogLevel.Error, $"{message}{Environment.NewLine}{exception}");
    }

    public Task CriticalAsync(string message, Exception exception)
    {
        _errorSubject.OnNext(new LogEntry(LogLevel.Critical, message, exception));
        return LogAsync(LogLevel.Critical, $"{message}{Environment.NewLine}{exception}");
    }

    /// <summary>Writes both halves of the log somewhere the DJ chose, without the DJ's name in it.</summary>
    /// <remarks>
    /// This is the copy that leaves the machine: the bug template asks for it in a public issue.
    /// The file on disk is left exactly as it was written, because there is nobody to hide it from
    /// on the machine it is about.
    /// </remarks>
    public Task ExportAsync(string path)
    {
        return Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                _logFile.Refresh();
                _previousLogFile.Refresh();
                if (!_logFile.Exists && !_previousLogFile.Exists)
                {
                    return;
                }

                // Oldest first, so the export reads as one run of time whichever side of a
                // rotation the failure that prompted it fell on.
                var text = await ReadOrEmptyAsync(_previousLogFile) + await ReadOrEmptyAsync(_logFile);
                await _logFile.FileSystem.File.WriteAllTextAsync(
                    path, LogPaths.WithoutUserProfile(text, _userProfile));
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    private static async Task<string> ReadOrEmptyAsync(IFileInfo file) =>
        file.Exists ? await file.FileSystem.File.ReadAllTextAsync(file.FullName) : "";

    private async Task WriteLineAsync(string line)
    {
        await _semaphore.WaitAsync();
        try
        {
            _logFile.Refresh();
            if (_logFile is { Exists: true, Length: >= MaxFileSizeBytes })
            {
                // Kept as the previous log rather than thrown away. Half a megabyte of an evening
                // fills in an hour, and the failure somebody exported the log for is as often as
                // not on the far side of the boundary.
                _logFile.FileSystem.File.Move(_logFile.FullName, _previousLogFile.FullName, overwrite: true);
                _logFile.Refresh();
            }

            await _logFile.FileSystem.File.AppendAllTextAsync(_logFile.FullName, line);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _errorSubject.Dispose();
        _semaphore.Dispose();
    }

    private static string FormatLine(LogLevel logLevel, string message) =>
        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel.ToString().ToUpperInvariant()}] {message}{Environment.NewLine}";
}
