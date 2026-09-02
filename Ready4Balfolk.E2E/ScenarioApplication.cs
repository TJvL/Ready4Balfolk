using System.IO.Abstractions;
using Avalonia;
using Avalonia.Headless;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.UI;

namespace Ready4Balfolk.E2E;

/// <summary>The application the headless session builds, once per scenario.</summary>
/// <remarks>
/// Everything but the windowing backend comes from <see cref="ApplicationComposition"/>, which is
/// the same call <c>Program</c> makes: the container a scenario clicks around in is the shipped one,
/// not a copy of it assembled here.
/// </remarks>
public static class ScenarioApplication
{
    /// <summary>
    /// The world the next application is built against, set by <see cref="HeadlessSession"/> before
    /// it dispatches.
    /// </summary>
    /// <remarks>
    /// A static, because the session builds the application itself and takes no argument through.
    /// Safe because scenarios never run side by side: see AssemblyInfo.
    /// </remarks>
    internal static ScenarioWorld? World { get; set; }

    /// <summary>Found by name and called by the headless session. Do not rename.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        ApplicationComposition.Configure(
            AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()),
            new ApplicationOptions
            {
                // No hardware to speak to on a build agent, and none needed: BASS decodes and keeps
                // time on its no sound device, so playback in a scenario is real but silent.
                UseNoSoundDevice = true,
                SettingsDirectory = new CurrentWorld()
            });

    /// <summary>Points at whichever world is running, rather than at the one that was.</summary>
    /// <remarks>
    /// The session builds this <c>AppBuilder</c> once and then stands up an application from it per
    /// scenario, so anything read here while the builder is being described is read before the
    /// first world exists. Handing over the world itself put every scenario back in the user's own
    /// profile directory, silently: the settings file the scenario had just written was not the one
    /// the application read.
    /// </remarks>
    private sealed class CurrentWorld : IApplicationSettingsDirectory
    {
        public IDirectoryInfo DirectoryInfoRoot =>
            (World ?? throw new InvalidOperationException("No scenario world is running.")).DirectoryInfoRoot;
    }
}
