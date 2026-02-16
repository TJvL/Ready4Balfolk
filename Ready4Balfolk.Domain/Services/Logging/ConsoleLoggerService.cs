using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ready4Balfolk.Domain.Services.Logging;

public sealed class ConsoleLoggerService : ILoggerService, IDisposable
{
    private readonly ReplaySubject<LogEntry> _errorSubject = new(bufferSize: 10);

    public IObservable<LogEntry> WhenErrorLogged => _errorSubject.AsObservable();

    public Task LogAsync(LogLevel logLevel, string message)
    {
        Console.WriteLine($"[{logLevel}] {message}");
        return Task.CompletedTask;
    }

    public Task DebugAsync(string message) => LogAsync(LogLevel.Debug, message);
    public Task InfoAsync(string message) => LogAsync(LogLevel.Info, message);
    public Task WarningAsync(string message) => LogAsync(LogLevel.Warning, message);

    public Task ErrorAsync(string message)
    {
        _errorSubject.OnNext(new LogEntry(LogLevel.Error, message));
        Console.WriteLine($"[Error] {message}");
        return Task.CompletedTask;
    }

    public Task ErrorAsync(string message, Exception exception)
    {
        _errorSubject.OnNext(new LogEntry(LogLevel.Error, message, exception));
        Console.WriteLine($"[Error] {message}: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task CriticalAsync(string message, Exception exception)
    {
        _errorSubject.OnNext(new LogEntry(LogLevel.Critical, message, exception));
        Console.WriteLine($"[Critical] {message}: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task ExportAsync(FileInfo fileInfo) => Task.CompletedTask;

    public void Dispose() => _errorSubject.Dispose();
}
