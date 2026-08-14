using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Hubs;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Tests.Unit;

public sealed class HostServiceForwardingTests
{
    /// <summary>
    /// Every constructor dependency of the hub must be resolvable in the web host's container.
    /// </summary>
    /// <remarks>
    /// The web host builds its own provider, so a service the desktop host has is invisible until
    /// it is forwarded. Missing one is silent at startup and only surfaces when a phone connects
    /// and every hub activation throws, which is how the pool once made the whole remote dead.
    /// </remarks>
    [Fact]
    public void TheRemoteHub_CanBeActivatedFromTheForwardedServices()
    {
        var host = new ServiceCollection();
        host.AddSingleton(Substitute.For<IPresentationStateService>());
        host.AddSingleton(Substitute.For<IQueueService>());
        host.AddSingleton(Substitute.For<IQueueConsumptionService>());
        host.AddSingleton(Substitute.For<IRandomTrackService>());
        host.AddSingleton(Substitute.For<IDancePool>());
        host.AddSingleton(Substitute.For<ITrackStore>());
        host.AddSingleton(Substitute.For<ISettingsStore>());
        host.AddSingleton(Substitute.For<ILoggerService>());
        host.AddSingleton(Substitute.For<IRemoteCommandDispatcher>());
        using var hostProvider = host.BuildServiceProvider();

        var web = new ServiceCollection();
        web.AddLogging();
        web.AddSignalR();
        web.AddForwardedHostServices(hostProvider);
        // What PresentationWebServer registers beside the forwards.
        web.AddSingleton(new RemoteAccessService());
        web.AddSingleton<PresentationBroadcaster>();
        using var webProvider = web.BuildServiceProvider();

        var constructor = typeof(RemoteHub).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        foreach (var parameter in constructor.GetParameters())
        {
            Assert.True(
                webProvider.GetService(parameter.ParameterType) is not null,
                $"RemoteHub needs {parameter.ParameterType.Name}, which the web host cannot resolve.");
        }
    }
}
