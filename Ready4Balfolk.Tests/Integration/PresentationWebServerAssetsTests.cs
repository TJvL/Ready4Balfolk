using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>What a browser pointed at the server actually receives.</summary>
/// <remarks>
/// The display page and its scripts are embedded in the Ready4Balfolk.Web assembly and served from
/// there rather than from disk, so nothing on the path from the file to the browser is visible in a
/// build that succeeded. The smoke test makes the same requests against every package for the same
/// reason; this is the cheap half of it, run on every pull request.
/// </remarks>
public sealed class PresentationWebServerAssetsTests
{
    [Fact]
    public async Task TheDisplayPage_IsServed()
    {
        await using var server = await RunningWebServer.StartAsync();

        using var client = new HttpClient();
        using var response = await client.GetAsync(
            new Uri($"http://127.0.0.1:{server.Port}/"), TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"the display page came back as {(int)response.StatusCode}");

        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("display.js", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page above is served by a route that reads display.html out of the assembly itself, so
    /// it proves nothing about the rest: the scripts and the stylesheet reach a browser through the
    /// static file middleware, and that is the path an embedding glob narrowed to the html files,
    /// or a UseStaticFiles registration lost while another endpoint was added, would take away.
    /// The page would still be served, and the hall would still get a blank projector.
    /// </summary>
    [Theory]
    [InlineData("display.js")]
    [InlineData("app.css")]
    [InlineData("strings.js")]
    [InlineData("remote.js")]
    public async Task EveryAssetThePagePullsIn_IsServed(string asset)
    {
        await using var server = await RunningWebServer.StartAsync();

        using var client = new HttpClient();
        using var response = await client.GetAsync(
            new Uri($"http://127.0.0.1:{server.Port}/{asset}"), TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{asset} came back as {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(body);
    }
}
