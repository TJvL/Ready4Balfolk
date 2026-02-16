using System.Reactive.Linq;

namespace Ready4Balfolk.Domain.Services.Logging;

public sealed class NoOpLoggerService : ILoggerService
{
    public IObservable<LogEntry> WhenErrorLogged => Observable.Empty<LogEntry>();

    public Task LogAsync(LogLevel logLevel, string message) => Task.CompletedTask;

    public Task DebugAsync(string message) => Task.CompletedTask;

    public Task InfoAsync(string message) => Task.CompletedTask;

    public Task WarningAsync(string message) => Task.CompletedTask;

    public Task ErrorAsync(string message) => Task.CompletedTask;

    public Task ErrorAsync(string message, Exception exception) => Task.CompletedTask;

    public Task CriticalAsync(string message, Exception exception) => Task.CompletedTask;

    public Task ExportAsync(FileInfo fileInfo) => Task.CompletedTask;
}
