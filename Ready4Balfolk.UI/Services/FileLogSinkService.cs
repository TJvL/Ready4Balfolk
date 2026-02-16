using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Logging;
using Ready4Balfolk.Domain.Services.Logging;
using LogLevel = Ready4Balfolk.Domain.Services.Logging.LogLevel;

namespace Ready4Balfolk.UI.Services;

internal sealed partial class FileLogSinkService(ILoggerService loggerService) : ILogSink
{
    private static readonly Dictionary<string, LogEventLevel> AreaMinimumLevels = new(StringComparer.Ordinal)
    {
        ["Layout"] = LogEventLevel.Warning,
        ["Binding"] = LogEventLevel.Error,
        ["IME"] = LogEventLevel.Fatal,
        ["Property"] = LogEventLevel.Warning,
        ["Visual"] = LogEventLevel.Warning,
        ["Animations"] = LogEventLevel.Warning,
    };

    private const LogEventLevel DefaultMinimumLevel = LogEventLevel.Warning;

    public bool IsEnabled(LogEventLevel level, string area) =>
        level >= AreaMinimumLevels.GetValueOrDefault(area, DefaultMinimumLevel);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        var message = $"[Avalonia:{area}] {messageTemplate}";
        loggerService.LogAsync(MapLevel(level), message);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate,
        params object?[] propertyValues)
    {
        var formatted = FormatTemplate(messageTemplate, propertyValues);
        var message = $"[Avalonia:{area}] {formatted}";
        loggerService.LogAsync(MapLevel(level), message);
    }

    private static LogLevel MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Debug,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Info,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.Debug,
    };

    private static string FormatTemplate(string template, object?[] values)
    {
        var index = 0;
        return PlaceholderRegex().Replace(template, match =>
            index < values.Length ? values[index++]?.ToString() ?? "null" : match.Value);
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex PlaceholderRegex();
}
