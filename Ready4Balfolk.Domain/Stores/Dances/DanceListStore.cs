using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Stores.Dances;

/// <summary>Owns the copy of BigBalfolkList the application is working from.</summary>
/// <remarks>
/// One source on startup: the copy on disk, put there by fetching it or by importing a file.
/// Nothing is shipped to fall back on, so a machine nobody has done either on has no vocabulary at
/// all, and says so. An update replaces the file whole, because the list is somebody else's and
/// there is nothing of the user's in it to merge around.
/// </remarks>
public sealed class DanceListStore(
    IApplicationSettingsDirectory dataDirectory,
    IFileSystem fileSystem,
    IDanceListFeed feed,
    ILoggerService loggerService)
    : IDanceListStore
{
    private const string DanceListFileName = "dance_list.json";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly BehaviorSubject<DanceList> _list = new(DanceList.Empty);
    private readonly BehaviorSubject<DanceListStatus> _status = new(DanceListStatus.Unknown);
    private readonly BehaviorSubject<bool> _isLoading = new(false);

    /// <summary>
    /// The bytes the current list came from. Compared rather than the parsed list, because the
    /// published file has a canonical formatting: same bytes means the same list, and it says so
    /// without walking a hundred dances.
    /// </summary>
    private string? _currentJson;

    private string DanceListFilePath => Path.Combine(dataDirectory.DirectoryInfoRoot.FullName, DanceListFileName);

    public IObservable<bool> IsLoading => _isLoading.AsObservable();

    public DanceList Current => _list.Value;

    public DanceListIndex Index { get; private set; } = DanceListIndex.Empty;

    public DanceListStatus Status => _status.Value;

    public IObservable<DanceList> Observe() => _list.AsObservable();

    public IObservable<DanceListStatus> ObserveStatus() => _status.AsObservable();

    public async Task LoadAsync(CancellationToken token)
    {
        _isLoading.OnNext(true);
        try
        {
            var cachedFileInfo = fileSystem.FileInfo.New(DanceListFilePath);
            if (cachedFileInfo.Exists)
            {
                try
                {
                    var json = await fileSystem.File.ReadAllTextAsync(cachedFileInfo.FullName, token);
                    var cached = DanceListReader.Read(json);
                    _currentJson = json;
                    Publish(cached, DanceListOrigin.Cached, cachedFileInfo.LastWriteTimeUtc);
                    return;
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException)
                {
                    Discard(cachedFileInfo, exception);
                }
            }

            // Nothing on disk and nothing shipped: the application has no dance vocabulary until
            // somebody fetches one or imports one, and everything that needs dances says so.
            Publish(DanceList.Empty, DanceListOrigin.None, obtainedAt: null);
        }
        finally
        {
            _isLoading.OnNext(false);
        }
    }

    public async Task<DanceListUpdate> RefreshAsync(CancellationToken token = default)
    {
        string published;
        try
        {
            published = await feed.DownloadAsync(token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Offline is an ordinary state for this application, so it is logged and reported, not
            // thrown: the list already loaded carries on working.
            _ = loggerService.InfoAsync($"Could not reach the dance list: {exception.Message}");
            return DanceListUpdate.Failed(exception.Message);
        }

        return await AdoptAsync(published, DanceListOrigin.Downloaded);
    }

    public async Task<DanceListUpdate> UpdateFromFileAsync(
        IFileInfo sourceFileInfo, CancellationToken token = default)
    {
        try
        {
            var json = await fileSystem.File.ReadAllTextAsync(sourceFileInfo.FullName, token);
            return await AdoptAsync(json, DanceListOrigin.File);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = loggerService.ErrorAsync($"Could not read {sourceFileInfo.FullName}", exception);
            return DanceListUpdate.Failed(exception.Message);
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        _list.Dispose();
        _status.Dispose();
        _isLoading.Dispose();
    }

    /// <summary>Validates a list, writes it as the cached copy, and publishes it.</summary>
    private async Task<DanceListUpdate> AdoptAsync(string json, DanceListOrigin origin)
    {
        await _gate.WaitAsync();
        try
        {
            DanceList list;
            try
            {
                list = DanceListReader.Read(json);
            }
            catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException)
            {
                _ = loggerService.ErrorAsync("Refused a dance list", exception);
                return DanceListUpdate.Failed(exception.Message);
            }

            var known = Current.Dances.Select(dance => dance.Slug).ToHashSet(StringComparer.Ordinal);
            var added = list.Dances.Count(dance => !known.Contains(dance.Slug));
            var identical = string.Equals(json, _currentJson, StringComparison.Ordinal);

            _currentJson = json;
            await SaveAsync(json);
            Publish(list, origin, DateTimeOffset.UtcNow);

            return identical ? DanceListUpdate.Unchanged : DanceListUpdate.Updated(added);
        }
        finally
        {
            _gate.Release();
        }
    }

    // The index is set before the list is published, so a subscriber reacting to the new list never
    // reads an index built from the old one.
    private void Publish(DanceList list, DanceListOrigin origin, DateTimeOffset? obtainedAt)
    {
        Index = DanceListIndex.Build(list);
        _list.OnNext(list);
        _status.OnNext(new DanceListStatus(list.Dances.Count, list.Tags.Count, origin, obtainedAt));
        _ = loggerService.InfoAsync(
            $"Dance list in use: {list.Dances.Count} dances, {list.Tags.Count} tags, from {origin}");
    }

    private async Task SaveAsync(string json)
    {
        try
        {
            dataDirectory.DirectoryInfoRoot.Create();
            await fileSystem.File.WriteAllTextAsync(DanceListFilePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The list is in hand either way; only the next start pays for this.
            _ = loggerService.ErrorAsync("Failed to cache the dance list", exception);
        }
    }

    /// <summary>
    /// Throws away a cached copy this build cannot read. Nothing of the user's is in it: it is a
    /// copy of a published file, and the next fetch replaces it. Keeping it would leave a hidden
    /// file nobody ever opens.
    /// </summary>
    private void Discard(IFileInfo cachedFileInfo, Exception reason)
    {
        try
        {
            cachedFileInfo.Delete();
            _ = loggerService.InfoAsync(
                $"The cached dance list could not be read and was discarded: {reason.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = loggerService.ErrorAsync("The cached dance list could not be read or removed", exception);
        }
    }
}
