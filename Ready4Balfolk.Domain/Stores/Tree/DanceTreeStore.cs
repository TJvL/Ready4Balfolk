using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Stores.Tree;

public sealed class DanceTreeStore(DirectoryInfo danceTreeDirectoryInfo) : IDanceTreeStore, IDisposable
{
    private const string DanceTreeFileName = "dance_tree.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<IReadOnlyList<DanceBranch>> _branches = new([]);

    private string DanceTreeFilePath => Path.Combine(danceTreeDirectoryInfo.FullName, DanceTreeFileName);

    public IReadOnlyList<DanceBranch> Current => _branches.Value;

    public IObservable<IReadOnlyList<DanceBranch>> Observe() => _branches.AsObservable();

    public async Task LoadAsync()
    {
        if (!File.Exists(DanceTreeFilePath))
            return;

        await using var stream = File.OpenRead(DanceTreeFilePath);
        var branches = await JsonSerializer.DeserializeAsync<List<DanceBranch>>(stream, JsonOptions);
        if (branches != null)
            _branches.OnNext(branches);
    }

    public async Task UpdateAsync(Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>> transform)
    {
        await _gate.WaitAsync();
        try
        {
            var updated = transform(Current);
            _branches.OnNext(updated);
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

        List<DanceBranch> branches;
        try
        {
            await using var stream = File.OpenRead(sourceFileInfo.FullName);
            branches = await JsonSerializer.DeserializeAsync<List<DanceBranch>>(stream, JsonOptions)
                       ?? throw new InvalidDataException(DomainStrings.ImportFileContainsNull);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(DomainStrings.DanceTreeStore_InvalidJson, ex);
        }

        if (branches.Any(b => string.IsNullOrWhiteSpace(b.Name)))
            throw new InvalidDataException(DomainStrings.DanceTreeStore_MissingNames);

        await _gate.WaitAsync();
        try
        {
            _branches.OnNext(branches);
            await SaveAsync(branches);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        _branches.Dispose();
    }

    private async Task SaveAsync(IReadOnlyList<DanceBranch> branches)
    {
        danceTreeDirectoryInfo.Create();
        await using var stream = File.Create(DanceTreeFilePath);
        await JsonSerializer.SerializeAsync(stream, branches, JsonOptions);
    }
}
