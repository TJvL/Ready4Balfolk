using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Stores.Settings;

public sealed class SettingsStore(DirectoryInfo settingsDirectoryInfo) : ISettingsStore, IDisposable
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<ApplicationSettings> _settings = new(LoadInitial(settingsDirectoryInfo));

    private string SettingsFilePath => Path.Combine(settingsDirectoryInfo.FullName, SettingsFileName);

    public ApplicationSettings Current => _settings.Value;

    public IObservable<ApplicationSettings> Observe() => _settings.AsObservable();

    private static ApplicationSettings LoadInitial(DirectoryInfo directory)
    {
        var path = Path.Combine(directory.FullName, SettingsFileName);
        if (!File.Exists(path))
            return new ApplicationSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ApplicationSettings>(json, JsonOptions) ?? new ApplicationSettings();
        }
        catch (JsonException)
        {
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
        settingsDirectoryInfo.Create();
        await using var stream = File.Create(SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
