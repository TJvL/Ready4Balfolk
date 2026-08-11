using Microsoft.Extensions.Logging;
using Ready4Balfolk.Domain.Services.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Ready4Balfolk.Web;

/// <summary>Sends ASP.NET's own logging into the app's log file.</summary>
/// <remarks>
/// The default console provider writes to a stdout that a windowed app does not have, so a Kestrel
/// failure would otherwise vanish. Warnings and worse only: the request log is noise here.
/// </remarks>
public sealed class AppLogBridgeProvider(ILoggerService logger) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BridgeLogger(logger, categoryName);

    public void Dispose()
    {
        // Nothing owned: the app's logger outlives every server instance.
    }

    private sealed class BridgeLogger(ILoggerService logger, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(MsLogLevel logLevel) => logLevel >= MsLogLevel.Warning;

        public void Log<TState>(
            MsLogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = $"[{category}] {formatter(state, exception)}";

            if (logLevel < MsLogLevel.Error)
            {
                _ = logger.WarningAsync(message);
                return;
            }

            _ = exception is null
                ? logger.ErrorAsync(message)
                : logger.ErrorAsync(message, exception);
        }
    }
}
