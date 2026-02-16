using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Stores.Settings;

public sealed class SettingsStore : ISettingsStore, IDisposable
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly DirectoryInfo _settingsDirectoryInfo;
    private readonly ILoggerService _loggerService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<ApplicationSettings> _settings;

    private string SettingsFilePath => Path.Combine(_settingsDirectoryInfo.FullName, SettingsFileName);

    public SettingsStore(DirectoryInfo settingsDirectoryInfo, ILoggerService? loggerService = null)
    {
        _settingsDirectoryInfo = settingsDirectoryInfo;
        _loggerService = loggerService ?? new NoOpLoggerService();
        _settings = new BehaviorSubject<ApplicationSettings>(LoadInitial(settingsDirectoryInfo, _loggerService));
    }

    public ApplicationSettings Current => _settings.Value;

    public IObservable<ApplicationSettings> Observe() => _settings.AsObservable();

    private static ApplicationSettings LoadInitial(FileSystemInfo directory, ILoggerService loggerService)
    {
        var path = Path.Combine(directory.FullName, SettingsFileName);
        if (!File.Exists(path))
        {
            return new ApplicationSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ApplicationSettings>(json, JsonOptions) ?? new ApplicationSettings();
        }
        catch (JsonException ex)
        {
            _ = loggerService.WarningAsync($"Corrupt settings file, using defaults: {ex.Message}");
            return new ApplicationSettings();
        }
    }

    public async Task UpdateAsync(Func<ApplicationSettings, ApplicationSettings> transform)
    {
        await _gate.WaitAsync();
        try
        {
            var updated = transform(Current);
            _settings.OnNext(updated);
            await SaveAsync(updated);
            _ = _loggerService.DebugAsync("Settings saved");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        _settings.Dispose();
    }

    private async Task SaveAsync(ApplicationSettings settings)
    {
        try
        {
            _settingsDirectoryInfo.Create();
            await using var stream = File.Create(SettingsFilePath);
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = _loggerService.ErrorAsync("Failed to save settings", ex);
        }
    }
}
