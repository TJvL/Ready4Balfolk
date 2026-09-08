using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// The other half of the leak fix's issue: a static asset served without a freshness directive lets
/// a browser cache it on its own heuristic, so a phone can go on serving a script from before the
/// last upgrade for days. Every file the pages actually load is asked for here, against a server
/// that really bound a port, and the answer is checked for the header that stops that guess.
/// </summary>
public sealed class PresentationWebServerCacheHeaderTests
{
    [Theory]
    [InlineData("app.css")]
    [InlineData("strings.js")]
    [InlineData("remote.js")]
    [InlineData("display.js")]
    [InlineData("lib/signalr.min.js")]
    public async Task AStaticAsset_IsServedWithNoCache(string path)
    {
        await using var server = await RunningWebServer.StartAsync();
        using var client = new HttpClient();

        using var response = await client.GetAsync(
            $"http://127.0.0.1:{server.Port}/{path}", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{path} did not come back: {response.StatusCode}");
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }
}
