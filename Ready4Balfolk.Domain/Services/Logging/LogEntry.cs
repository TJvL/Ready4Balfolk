namespace Ready4Balfolk.Domain.Services.Logging;

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception = null);
