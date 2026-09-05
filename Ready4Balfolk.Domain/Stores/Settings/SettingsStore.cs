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

    /// <summary>What an unreadable settings file is kept as, so it can be looked at rather than lost.</summary>
    private const string CorruptSuffix = ".corrupt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // A field this build does not know is not a reason to throw away the ones it does.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        Converters =
        {
            new LenientEnumConverter()
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

    /// <summary>Reads the settings file, and never throws: this runs while the application is being composed.</summary>
    /// <remarks>
    /// A throw here is not a settings problem, it is no window at all, so everything the disk can
    /// answer with is caught: a file another process is holding open raises an IOException, not a
    /// JsonException. A file that parsed as nothing readable is kept beside the real one instead of
    /// being written over on the next save, because it is a file the user is invited to edit by hand
    /// and it is the only copy of what they had.
    /// </remarks>
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
            _ = loggerService.ErrorAsync($"Unreadable settings file, starting from defaults: {ex.Message}");
            Quarantine(path, fileSystem, loggerService);
            return new ApplicationSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file is there and may be perfectly good, so it is left exactly as it is.
            _ = loggerService.ErrorAsync("Could not open the settings file, starting from defaults", ex);
            return new ApplicationSettings();
        }
    }

    /// <summary>Moves an unreadable settings file aside under a fixed name, or leaves it where it is.</summary>
    private static void Quarantine(string path, IFileSystem fileSystem, ILoggerService loggerService)
    {
        try
        {
            // One name, overwritten: a run of bad starts leaves one file to look at rather than a
            // pile of them, and asking for a free name is another thing that can throw.
            fileSystem.File.Move(path, path + CorruptSuffix, overwrite: true);
            _ = loggerService.ErrorAsync($"The settings file was kept as {path + CorruptSuffix}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = loggerService.ErrorAsync("The unreadable settings file could not be moved aside", ex);
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
