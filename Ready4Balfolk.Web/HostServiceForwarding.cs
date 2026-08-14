using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Web;

/// <summary>Hands the running app's services to the web host without building a second set.</summary>
public static class HostServiceForwarding
{
    /// <summary>
    /// Registers the app's own singletons into the web host's container, as instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>WebApplication.CreateSlimBuilder</c> builds its own <see cref="IServiceProvider"/>. Every
    /// registration below is therefore a forward of an already-constructed object, never a type.
    /// </para>
    /// <para>
    /// <b>Do not replace any of these with <c>AddSingleton&lt;TService, TImplementation&gt;</c>.</b>
    /// That would construct a second queue and a second audio engine inside the web host, and the
    /// browser would faithfully render a queue that nothing is playing. It fails silently, which is
    /// exactly how the same mistake cost an evening in #47.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddForwardedHostServices(
        this IServiceCollection services,
        IServiceProvider hostServices)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(hostServices);

        services.AddSingleton(hostServices.GetRequiredService<IPresentationStateService>());
        services.AddSingleton(hostServices.GetRequiredService<IQueueService>());
        services.AddSingleton(hostServices.GetRequiredService<IQueueConsumptionService>());
        services.AddSingleton(hostServices.GetRequiredService<IRandomTrackService>());
        services.AddSingleton(hostServices.GetRequiredService<IDancePool>());
        services.AddSingleton(hostServices.GetRequiredService<ITrackStore>());
        services.AddSingleton(hostServices.GetRequiredService<ISettingsStore>());
        services.AddSingleton(hostServices.GetRequiredService<ILoggerService>());
        services.AddSingleton(hostServices.GetRequiredService<IRemoteCommandDispatcher>());

        return services;
    }
}
