namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>Where the published dance list is downloaded from.</summary>
public interface IDanceListFeed
{
    /// <summary>The page a human should read, shown next to the list rather than fetched.</summary>
    Uri HomePage { get; }

    /// <summary>The list as it is published right now, unparsed.</summary>
    /// <exception cref="HttpRequestException">It could not be reached.</exception>
    /// <exception cref="TaskCanceledException">It did not answer in time.</exception>
    Task<string> DownloadAsync(CancellationToken token = default);
}
