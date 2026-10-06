using System.Reflection;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web.Contracts;

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

    /// <summary>
    /// The hub sends its own refusals as a code and the phone page words them, so a code with no
    /// string behind it reaches the helper as the bare code. Read from the strings.js a browser is
    /// actually served, once for each language table in it.
    /// </summary>
    [Fact]
    public async Task EveryRefusalTheHubSends_HasWordsInBothLanguages()
    {
        await using var server = await RunningWebServer.StartAsync();

        using var client = new HttpClient();
        var script = await client.GetStringAsync(
            new Uri($"http://127.0.0.1:{server.Port}/strings.js"), TestContext.Current.CancellationToken);

        var english = script.IndexOf("en: {", StringComparison.Ordinal);
        var dutch = script.IndexOf("nl: {", StringComparison.Ordinal);
        Assert.True(english >= 0 && dutch > english, "strings.js no longer has an en table and then an nl one");

        var tables = new Dictionary<string, string>
        {
            ["en"] = script[english..dutch],
            ["nl"] = script[dutch..script.IndexOf("};", dutch, StringComparison.Ordinal)]
        };

        var codes = typeof(RemoteRefusal)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
        Assert.NotEmpty(codes);

        foreach (var (language, table) in tables)
        {
            foreach (var code in codes)
            {
                Assert.True(
                    table.Contains($"{code}: \"", StringComparison.Ordinal),
                    $"strings.js has no {language} words for the refusal {code}");
            }
        }
    }
}
