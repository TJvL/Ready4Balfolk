using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

public sealed class ApplicationCompositionTests
{
    /// <summary>
    /// Every dialog belongs to the one window startup hands to <see cref="DialogOwner"/>, so there
    /// has to be one owner for all of them to read.
    /// </summary>
    /// <remarks>
    /// Runs the real <see cref="ApplicationComposition.ConfigureServices"/>. A second registration
    /// would give some dialogs an owner nobody ever set, and those would quietly never open: the
    /// confirmation answers yes, the picker returns nothing, the editor does nothing.
    /// </remarks>
    [Fact]
    public void EveryDialogService_ReadsTheOneOwner()
    {
        var services = new ServiceCollection();
        var options = new ApplicationOptions
        {
            // The concrete stores the editor depends on need a real settings directory and a real
            // database; registered last, per ApplicationOptions.AlsoRegister, these substitutes win
            // over ConfigureServices' own registrations without either ever being asked to resolve.
            AlsoRegister = s =>
            {
                s.AddSingleton(Substitute.For<IDanceListStore>());
                s.AddSingleton(Substitute.For<ILibraryIndex>());
                s.AddSingleton(Substitute.For<ITrackStore>());
            }
        };

        ApplicationComposition.ConfigureServices(services, options);
        using var provider = services.BuildServiceProvider();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DialogOwner));
        Assert.Same(provider.GetRequiredService<DialogOwner>(), provider.GetRequiredService<DialogOwner>());

        // Each of these takes the owner in its constructor, so resolving them is what proves they
        // can all be built around the one instance above.
        Assert.NotNull(provider.GetRequiredService<IConfirmationService>());
        Assert.NotNull(provider.GetRequiredService<IFilePickerService>());
        Assert.NotNull(provider.GetRequiredService<ITrackEditorService>());
        Assert.NotNull(provider.GetRequiredService<IDialogService>());
    }

    /// <summary>
    /// The domain tells the DJ through the interface it declares, and what it reaches is the very
    /// list the overlay draws.
    /// </summary>
    /// <remarks>
    /// A second instance behind the interface would take every failure a store or the audio engine
    /// reports and put it on a list nothing is bound to: the DJ would be told nothing, silently,
    /// which is the failure this whole arrangement exists to end.
    /// </remarks>
    [Fact]
    public void Notifications_AreTheSameInstanceUnderTheDomainsInterface()
    {
        var services = new ServiceCollection();
        var options = new ApplicationOptions
        {
            AlsoRegister = s =>
            {
                s.AddSingleton(Substitute.For<IDanceListStore>());
                s.AddSingleton(Substitute.For<ILibraryIndex>());
                s.AddSingleton(Substitute.For<ITrackStore>());
            }
        };

        ApplicationComposition.ConfigureServices(services, options);
        using var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<NotificationService>(),
            provider.GetRequiredService<INotificationService>());
    }
}
