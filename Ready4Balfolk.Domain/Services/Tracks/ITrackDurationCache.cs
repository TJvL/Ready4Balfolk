namespace Ready4Balfolk.Domain.Services.Tracks;

public interface ITrackDurationCache
{
    Task LoadAsync();

    TimeSpan? TryGetDuration(string filePath, DateTime lastWriteTimeUtc);

    void SetDuration(string filePath, DateTime lastWriteTimeUtc, TimeSpan duration);

    Task SaveAsync(HashSet<string> existingFilePaths);
}
