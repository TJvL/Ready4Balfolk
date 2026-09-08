using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

public sealed class ApplicationCompositionTests
{
    /// <summary>
    /// <see cref="ITrackEditorService"/> exists so a caller can depend on the interface rather than
    /// the concrete <see cref="TrackEditorService"/>, and it forwards to the same singleton
    /// (<c>SetOwner</c> configures one owner window; two instances would mean the wrong one answers
    /// a dialog).
    /// </summary>
    /// <remarks>
    /// Runs the real <see cref="ApplicationComposition.ConfigureServices"/>, not a copy of its
    /// forwarding line, so deleting that line or turning it back into a second registration fails
    /// this test rather than only showing up when a scenario exercises the edit dialog.
    /// </remarks>
    [Fact]
    public void TrackEditorService_IsRegisteredAsTheSameInstanceUnderItsInterface()
    {
        var services = new ServiceCollection();
        var options = new ApplicationOptions
        {
            // The concrete stores this service depends on need a real settings directory and a real
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

        var concrete = provider.GetRequiredService<TrackEditorService>();
        var viaInterface = provider.GetRequiredService<ITrackEditorService>();

        Assert.Same(concrete, viaInterface);
    }
}
