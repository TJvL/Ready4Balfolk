using System.IO.Abstractions;
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

    private readonly IDirectoryInfo _settingsDirectoryInfo;
    private readonly IFileSystem _fileSystem;
    private readonly ILoggerService _loggerService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<ApplicationSettings> _settings;

    private string SettingsFilePath => Path.Combine(_settingsDirectoryInfo.FullName, SettingsFileName);

    public SettingsStore(
        IApplicationSettingsDirectory settingsDirectoryInfo,
        IFileSystem fileSystem,
        ILoggerService? loggerService = null)
    {
        _settingsDirectoryInfo = settingsDirectoryInfo.DirectoryInfoRoot;
        _fileSystem = fileSystem;
        _loggerService = loggerService ?? new NoOpLoggerService();
        _settings = new BehaviorSubject<ApplicationSettings>(
            LoadInitial(_settingsDirectoryInfo, fileSystem, _loggerService));
    }

    public ApplicationSettings Current => _settings.Value;

    public IObservable<ApplicationSettings> Observe() => _settings.AsObservable();

    private static ApplicationSettings LoadInitial(
        IFileSystemInfo directory, IFileSystem fileSystem, ILoggerService loggerService)
    {
        var path = Path.Combine(directory.FullName, SettingsFileName);
        if (!fileSystem.File.Exists(path))
        {
            return new ApplicationSettings();
        }

        try
        {
            using var stream = fileSystem.FileStream.New(path, FileMode.Open, FileAccess.Read);
            return JsonSerializer.Deserialize<ApplicationSettings>(stream, JsonOptions) ?? new ApplicationSettings();
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

    /// <summary>Writes the settings out, all of them or none of them.</summary>
    /// <remarks>
    /// Serialised into a temporary file beside the real one and then moved over it, because a move
    /// within a directory is atomic and File.Create is not: it truncates first, so a crash or a
    /// flat battery part way through left a half written file. Loading treats a file it cannot
    /// parse as absent and carries on with defaults, so the visible symptom of that was every
    /// setting silently back to its factory value.
    /// </remarks>
    private async Task SaveAsync(ApplicationSettings settings)
    {
        try
        {
            _settingsDirectoryInfo.Create();
            var temporaryPath = SettingsFilePath + ".tmp";

            await using (var stream = _fileSystem.File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
            }

            _fileSystem.File.Move(temporaryPath, SettingsFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = _loggerService.ErrorAsync("Failed to save settings", ex);
        }
    }
}
