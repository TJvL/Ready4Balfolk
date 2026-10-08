using System.Net.Http.Headers;
using System.Reflection;

namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>BigBalfolkList, read straight from the repository it lives in.</summary>
/// <remarks>
/// The raw file rather than the site: it is the same bytes the project publishes, with no page
/// around it and nothing to parse out. Caching is deliberately turned off, because the whole reason
/// to press update is that something was merged a minute ago.
/// </remarks>
public sealed class DanceListFeed : IDanceListFeed, IDisposable
{
    private static readonly Uri ListUri =
        new("https://raw.githubusercontent.com/TJvL/BigBalfolkList/main/dances.json");

    /// <summary>The version of this build, as the user agent says it.</summary>
    /// <remarks>
    /// The build's own rather than a number written here, which said 1.0 for every release there
    /// has been. Without the commit after the plus, which says nothing to whoever reads a request.
    /// </remarks>
    private static string ProductVersion { get; } =
        typeof(DanceListFeed).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] is { Length: > 0 } version
            ? version
            : "dev";

    private readonly HttpClient _client;

    public DanceListFeed() : this(new HttpClientHandler())
    {
    }

    /// <summary>Takes the handler to send through.</summary>
    /// <remarks>
    /// The only reason this exists is that a feed which builds its own handler cannot be tested
    /// without reaching the network, and what is worth testing here is the behaviour around the
    /// request rather than GitHub being up.
    /// </remarks>
    public DanceListFeed(HttpMessageHandler handler)
    {
        _client = new HttpClient(handler)
        {
            // Long enough for a slow hall connection, short enough that startup is never held up.
            Timeout = TimeSpan.FromSeconds(15)
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ready4Balfolk", ProductVersion));
        _client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }

    public Uri HomePage { get; } = new("https://tjvl.github.io/BigBalfolkList/");

    public async Task<string> DownloadAsync(CancellationToken token = default)
    {
        using var response = await _client.GetAsync(ListUri, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token);
    }

    public void Dispose() => _client.Dispose();
}
