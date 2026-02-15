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
    private readonly BehaviorSubject<ApplicationSettings> _settings = new(new ApplicationSettings());

    private string SettingsFilePath => Path.Combine(settingsDirectoryInfo.FullName, SettingsFileName);

    public ApplicationSettings Current => _settings.Value;

    public IObservable<ApplicationSettings> Observe() => _settings.AsObservable();

    public async Task LoadAsync()
    {
        if (!File.Exists(SettingsFilePath))
            return;

        await using var stream = File.OpenRead(SettingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, JsonOptions);
        if (settings != null)
            _settings.OnNext(settings);
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
