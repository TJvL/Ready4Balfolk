using System.Net;
using Ready4Balfolk.Domain.Services.Dances;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Fetching the published dance list.
/// </summary>
/// <remarks>
/// Nothing here reaches the network: the handler is stubbed, and what is worth pinning down is the
/// behaviour around the request. A hall with no wifi is the normal case, not the exceptional one.
/// </remarks>
public sealed class DanceListFeedTests
{
    /// <summary>A handler that answers without a socket, and records what it was asked.</summary>
    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request, cancellationToken));
        }
    }

    private static StubHandler Responding(HttpStatusCode status, string body = "") =>
        new((_, _) => new HttpResponseMessage(status) { Content = new StringContent(body) });

    [Fact]
    public async Task DownloadAsync_Success_ReturnsTheBodyUntouched()
    {
        // The published file is read as bytes and handed on: parsing is DanceListReader's job, and
        // the feed knowing anything about the shape would be a second place to keep it right.
        const string published = """{"tags":["common"],"dances":[{"slug":"mazurka"}]}""";
        using var handler = Responding(HttpStatusCode.OK, published);
        using var sut = new DanceListFeed(handler);

        var json = await sut.DownloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(published, json);
    }

    [Fact]
    public async Task DownloadAsync_NotFound_Throws()
    {
        // A 404 body would parse as an empty list and quietly replace a good one.
        using var handler = Responding(HttpStatusCode.NotFound, "no such file");
        using var sut = new DanceListFeed(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.DownloadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_ServerError_Throws()
    {
        using var handler = Responding(HttpStatusCode.InternalServerError);
        using var sut = new DanceListFeed(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.DownloadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_AsksForTheRawFileAndRefusesACachedAnswer()
    {
        // The whole reason to press update is that something was merged a minute ago, so a cached
        // answer is the one thing that must not come back.
        using var handler = Responding(HttpStatusCode.OK, "{}");
        using var sut = new DanceListFeed(handler);

        await sut.DownloadAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("BigBalfolkList", request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("dances.json", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
        Assert.True(request.Headers.CacheControl?.NoCache);
        Assert.Contains(request.Headers.UserAgent,
            product => product.Product?.Name == "Ready4Balfolk");
    }

    [Fact]
    public async Task DownloadAsync_Cancelled_Throws()
    {
        using var handler = new StubHandler((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        using var sut = new DanceListFeed(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.DownloadAsync(cancellation.Token));
    }

    [Fact]
    public void HomePage_IsThePublishedSiteRatherThanTheRawFile()
    {
        // What the wizard offers to open, so it has to be the readable page.
        using var handler = Responding(HttpStatusCode.OK);
        using var sut = new DanceListFeed(handler);

        Assert.Contains("BigBalfolkList", sut.HomePage.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("raw.githubusercontent", sut.HomePage.ToString(), StringComparison.Ordinal);
    }
}
