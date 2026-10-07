namespace Ready4Balfolk.Domain.Services.Logging;

/// <summary>The application's log, which is English and is never shown to the DJ.</summary>
/// <remarks>
/// Nothing written here reaches the screen, whatever its level. A failure the DJ has to hear about
/// is told through <see cref="Notifications.INotificationService" /> with a text from the resx
/// files, beside the line written here: see <see cref="LoggerServiceExtensions" />.
/// </remarks>
public interface ILoggerService
{
    Task LogAsync(LogLevel logLevel, string message);
    Task DebugAsync(string message);
    Task InfoAsync(string message);
    Task WarningAsync(string message);
    Task ErrorAsync(string message);
    Task ErrorAsync(string message, Exception exception);
    Task CriticalAsync(string message, Exception exception);
    Task ExportAsync(string path);
}
