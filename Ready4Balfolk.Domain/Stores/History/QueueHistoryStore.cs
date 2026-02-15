using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.History;

namespace Ready4Balfolk.Domain.Stores.History;

public sealed class QueueHistoryStore(DirectoryInfo queueHistoryDirectoryInfo) : IQueueHistoryStore, IDisposable
{
    private const string QueueHistoryFileName = "queue_history.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<QueueHistory> _history = new(new QueueHistory(null, []));

    private string QueueHistoryFilePath =>
        Path.Combine(queueHistoryDirectoryInfo.FullName, QueueHistoryFileName);

    public QueueHistory Current => _history.Value;

    public IObservable<QueueHistory> Observe() => _history.AsObservable();

    public async Task LoadAsync()
    {
        if (!File.Exists(QueueHistoryFilePath))
            return;

        await using var stream = File.OpenRead(QueueHistoryFilePath);
        var history = await JsonSerializer.DeserializeAsync<QueueHistory>(stream, JsonOptions);
        if (history != null)
            _history.OnNext(history);
    }

    public async Task AddAsync(QueueHistoryEntry entry)
    {
        await _gate.WaitAsync();
        try
        {
            var current = Current;
            var entries = new List<QueueHistoryEntry>(current.Entries) { entry };
            var startedAt = current.StartedAt ?? DateTime.Now;
            var updated = new QueueHistory(startedAt, entries);
            _history.OnNext(updated);
            await SaveAsync(updated);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var updated = new QueueHistory(null, []);
            _history.OnNext(updated);
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

    public void Dispose()
    {
        _gate.Dispose();
        _history.Dispose();
    }

    private async Task SaveAsync(QueueHistory history)
    {
        queueHistoryDirectoryInfo.Create();
        await using var stream = File.Create(QueueHistoryFilePath);
        await JsonSerializer.SerializeAsync(stream, history, JsonOptions);
    }
}
