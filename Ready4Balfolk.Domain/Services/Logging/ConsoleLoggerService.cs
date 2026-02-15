namespace Ready4Balfolk.Domain.Services.Logging;

public sealed class ConsoleLoggerService : ILoggerService
{
    public Task LogAsync(LogLevel logLevel, string message)
    {
        Console.WriteLine($"[{logLevel}] {message}");
        return Task.CompletedTask;
    }

    public Task DebugAsync(string message) => LogAsync(LogLevel.Debug, message);
    public Task InfoAsync(string message) => LogAsync(LogLevel.Info, message);
    public Task WarningAsync(string message) => LogAsync(LogLevel.Warning, message);
    public Task ErrorAsync(string message) => LogAsync(LogLevel.Error, message);

    public Task ErrorAsync(string message, Exception exception)
    {
        Console.WriteLine($"[Error] {message}: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task CriticalAsync(string message, Exception exception)
    {
        Console.WriteLine($"[Critical] {message}: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task ExportAsync(FileInfo fileInfo) => Task.CompletedTask;
}
