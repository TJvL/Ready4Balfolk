namespace Ready4Balfolk.Domain.Services.Logging;

public interface ILoggerService
{
    Task LogAsync(LogLevel logLevel, string message);
    Task DebugAsync(string message);
    Task InfoAsync(string message);
    Task WarningAsync(string message);
    Task ErrorAsync(string message);
    Task ErrorAsync(string message, Exception exception);
    Task CriticalAsync(string message, Exception exception);
    Task ExportAsync(FileInfo fileInfo);
}
