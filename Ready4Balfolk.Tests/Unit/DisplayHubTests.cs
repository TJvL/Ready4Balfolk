using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Contracts;
using Ready4Balfolk.Web.Hubs;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The display page's end of the connection.
/// </summary>
/// <remarks>
/// A display is left running in the corner of a hall on whatever machine was to hand, on a page
/// nobody signed in to. That it can only listen is the reason it needs no PIN, so it is a property
/// of the class rather than a description of it, and asserted here as one.
/// </remarks>
public sealed class DisplayHubTests : IDisposable
{
    private readonly IPresentationStateService _state = Substitute.For<IPresentationStateService>();
    private readonly ISingleClientProxy _caller = Substitute.For<ISingleClientProxy>();
    private readonly PresentationBroadcaster _broadcaster;
    private readonly DisplayHub _sut;

    public DisplayHubTests()
    {
        _state.Current.Returns(new PresentationState(
            new PresentationItem(PresentationItemKind.Track, "Mazurka", "Naragonia", "Salamandre"),
            PresentationItem.None,
            PresentationItem.None,
            IsPlaying: true));

        // The real broadcaster: it is sealed, and it is only asked for its current picture here.
        _broadcaster = new PresentationBroadcaster(
            _state,
            Substitute.For<IQueueService>(),
            Substitute.For<IHubContext<DisplayHub>>(),
            Substitute.For<IHubContext<RemoteHub>>());

        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(_caller);
        _sut = new DisplayHub(_broadcaster) { Clients = clients };
    }

    [Fact]
    public async Task OnConnectedAsync_DrawsThePageImmediately()
    {
        // Without this a display that connects between two track changes stays blank until the
        // next one, which in a hall is minutes.
        await _sut.OnConnectedAsync();

        await _caller.Received(1).SendCoreAsync(
            DisplayHub.SnapshotMethod,
            Arg.Is<object?[]>(arguments => ((PresentationSnapshotDto)arguments[0]!).Current.Primary == "Mazurka"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheHub_OffersNothingThatCanChangeAnything()
    {
        // The display needs no PIN because there is nothing behind it to protect. A command method
        // added here would be reachable by anyone who can reach the page, and would look like an
        // ordinary hub method while it did it.
        var declared = typeof(DisplayHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name);

        Assert.Equal([nameof(DisplayHub.OnConnectedAsync)], declared);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _broadcaster.Dispose();
    }
}
