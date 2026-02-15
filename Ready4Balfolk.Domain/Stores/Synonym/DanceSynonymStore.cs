using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Stores.Synonym;

public sealed class DanceSynonymStore(DirectoryInfo danceSynonymDirectoryInfo) : IDanceSynonymStore, IDisposable
{
    private const string DanceSynonymsFileName = "dance_synonyms.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<IReadOnlyList<DanceMainName>> _synonyms = new([]);

    private string DanceSynonymsFilePath =>
        Path.Combine(danceSynonymDirectoryInfo.FullName, DanceSynonymsFileName);

    public IReadOnlyList<DanceMainName> Current => _synonyms.Value;

    public IObservable<IReadOnlyList<DanceMainName>> Observe() => _synonyms.AsObservable();

    public async Task LoadAsync()
    {
        if (!File.Exists(DanceSynonymsFilePath))
            return;

        await using var stream = File.OpenRead(DanceSynonymsFilePath);
        var synonyms = await JsonSerializer.DeserializeAsync<List<DanceMainName>>(stream, JsonOptions);
        if (synonyms != null)
            _synonyms.OnNext(synonyms);
    }

    public async Task UpdateAsync(Func<IReadOnlyList<DanceMainName>, IReadOnlyList<DanceMainName>> transform)
    {
        await _gate.WaitAsync();
        try
        {
            var updated = transform(Current);
            _synonyms.OnNext(updated);
            await SaveAsync(updated);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ExportAsync(FileInfo destinationFileInfo)
    {
        await _gate.WaitAsync();
        try
        {
            destinationFileInfo.Directory?.Create();
            await using var stream = File.Create(destinationFileInfo.FullName);
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ImportAsync(FileInfo sourceFileInfo)
    {
        if (!sourceFileInfo.Exists)
            throw new FileNotFoundException(DomainStrings.ImportFileNotFound, sourceFileInfo.FullName);

        List<DanceMainName> synonyms;
        try
        {
            await using var stream = File.OpenRead(sourceFileInfo.FullName);
            synonyms = await JsonSerializer.DeserializeAsync<List<DanceMainName>>(stream, JsonOptions)
                       ?? throw new InvalidDataException(DomainStrings.ImportFileContainsNull);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(DomainStrings.DanceSynonymStore_InvalidJson, ex);
        }

        if (synonyms.Any(s => string.IsNullOrWhiteSpace(s.Name)))
            throw new InvalidDataException(DomainStrings.DanceSynonymStore_MissingNames);

        var allNames = synonyms
            .SelectMany(m => new[] { m.Name }.Concat(m.Synonyms.Select(s => s.Name)))
            .Select(StringNormalizer.Normalize)
            .ToList();
        var duplicates = allNames
            .GroupBy(n => n, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidDataException(
                string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceSynonymStore_DuplicateNames, string.Join(", ", duplicates)));
        }

        await _gate.WaitAsync();
        try
        {
            _synonyms.OnNext(synonyms);
            await SaveAsync(synonyms);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        _synonyms.Dispose();
    }

    private async Task SaveAsync(IReadOnlyList<DanceMainName> synonyms)
    {
        danceSynonymDirectoryInfo.Create();
        await using var stream = File.Create(DanceSynonymsFilePath);
        await JsonSerializer.SerializeAsync(stream, synonyms, JsonOptions);
    }
}
