namespace Ready4Balfolk.Domain.Services.Logging;

public sealed class FileLoggerService : ILoggerService, IDisposable
{
    private const string LogFileName = "app.log";
    private const long MaxFileSizeBytes = 512 * 1024;

    private readonly FileInfo _logFile;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public FileLoggerService(DirectoryInfo logDirectory)
    {
        logDirectory.Create();
        _logFile = new FileInfo(Path.Combine(logDirectory.FullName, LogFileName));
    }

    public Task LogAsync(LogLevel logLevel, string message)
    {
        if (logLevel < MinimumLevel)
            return Task.CompletedTask;

        var line = FormatLine(logLevel, message);
        return Task.Run(() => WriteLineAsync(line));
    }

    public Task DebugAsync(string message) => LogAsync(LogLevel.Debug, message);

    public Task InfoAsync(string message) => LogAsync(LogLevel.Info, message);

    public Task WarningAsync(string message) => LogAsync(LogLevel.Warning, message);

    public Task ErrorAsync(string message) => LogAsync(LogLevel.Error, message);

    public Task ErrorAsync(string message, Exception exception) =>
        LogAsync(LogLevel.Error, $"{message}{Environment.NewLine}{exception}");

    public Task CriticalAsync(string message, Exception exception) =>
        LogAsync(LogLevel.Critical, $"{message}{Environment.NewLine}{exception}");

    public Task ExportAsync(FileInfo fileInfo)
    {
        return Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                _logFile.Refresh();
                if (_logFile.Exists)
                    _logFile.CopyTo(fileInfo.FullName, overwrite: true);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    private async Task WriteLineAsync(string line)
    {
        await _semaphore.WaitAsync();
        try
        {
            _logFile.Refresh();
            if (_logFile is { Exists: true, Length: >= MaxFileSizeBytes })
                _logFile.Delete();

            await File.AppendAllTextAsync(_logFile.FullName, line);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();

    private static string FormatLine(LogLevel logLevel, string message) =>
        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel.ToString().ToUpperInvariant()}] {message}{Environment.NewLine}";
}
