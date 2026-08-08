using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Stores.Dances;

/// <summary>Owns <c>dance_list.json</c>, the one place dance names live.</summary>
public sealed class DanceListStore(IApplicationSettingsDirectory dataDirectory, ILoggerService loggerService)
    : IDanceListStore
{
    private const string DanceListFileName = "dance_list.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<DanceList> _list = new(DanceList.Empty);
    private readonly BehaviorSubject<bool> _isLoading = new(false);

    private string DanceListFilePath => Path.Combine(dataDirectory.DirectoryInfoRoot.FullName, DanceListFileName);

    public IObservable<bool> IsLoading => _isLoading.AsObservable();

    public DanceList Current => _list.Value;

    public DanceListIndex Index { get; private set; } = DanceListIndex.Empty;

    public IObservable<DanceList> Observe() => _list.AsObservable();

    public async Task LoadAsync(CancellationToken token)
    {
        _isLoading.OnNext(true);
        try
        {
            if (!File.Exists(DanceListFilePath))
            {
                // No list yet is the ordinary state before the setup wizard has run, not a failure.
                return;
            }

            await using var stream = new FileStream(DanceListFilePath, FileMode.Open, FileAccess.Read);
            var list = await JsonSerializer.DeserializeAsync<DanceList>(stream, JsonOptions, token);
            if (list is not null)
            {
                Publish(list);
                _ = loggerService.InfoAsync(
                    $"Loaded dance list ({list.Categories.Count} categories, {list.AllDances.Count()} dances)");
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _ = loggerService.ErrorAsync("Failed to load dance list", ex);
        }
        finally
        {
            _isLoading.OnNext(false);
        }
    }

    public async Task UpdateAsync(Func<DanceList, DanceList> transform)
    {
        await _gate.WaitAsync();
        try
        {
            var updated = transform(Current);
            Publish(updated);
            await SaveAsync(updated);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ReplaceAsync(DanceList list) => UpdateAsync(_ => list);

    public async Task ExportAsync(FileInfo destinationFileInfo)
    {
        await _gate.WaitAsync();
        try
        {
            destinationFileInfo.Directory?.Create();
            await using var stream = File.Create(destinationFileInfo.FullName);
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions);
            _ = loggerService.InfoAsync($"Exported dance list to {destinationFileInfo.FullName}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ImportAsync(FileInfo sourceFileInfo)
    {
        if (!sourceFileInfo.Exists)
        {
            throw new FileNotFoundException(DomainStrings.ImportFileNotFound, sourceFileInfo.FullName);
        }

        DanceList list;
        try
        {
            await using var stream = File.OpenRead(sourceFileInfo.FullName);
            list = await JsonSerializer.DeserializeAsync<DanceList>(stream, JsonOptions)
                   ?? throw new InvalidDataException(DomainStrings.ImportFileContainsNull);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(DomainStrings.DanceListStore_InvalidJson, exception);
        }

        var problems = DanceListValidation.Validate(list);
        if (problems.DuplicateNames.Count > 0)
        {
            throw new InvalidDataException(string.Format(
                CultureInfo.CurrentCulture,
                DomainStrings.DanceListStore_DuplicateNames,
                string.Join(", ", problems.DuplicateNames.Distinct(StringComparer.Ordinal))));
        }

        if (problems.Any)
        {
            throw new InvalidDataException(DomainStrings.DanceListStore_InvalidJson);
        }

        await ReplaceAsync(list);
        _ = loggerService.InfoAsync($"Imported dance list from {sourceFileInfo.FullName}");
    }

    public void Dispose()
    {
        _gate.Dispose();
        _list.Dispose();
        _isLoading.Dispose();
    }

    // The index is set before the list is published, so a subscriber reacting to the new list never
    // reads an index built from the old one.
    private void Publish(DanceList list)
    {
        Index = DanceListIndex.Build(list);
        _list.OnNext(list);
    }

    private async Task SaveAsync(DanceList list)
    {
        try
        {
            dataDirectory.DirectoryInfoRoot.Create();
            await using var stream = File.Create(DanceListFilePath);
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = loggerService.ErrorAsync("Failed to save dance list", ex);
        }
    }
}
