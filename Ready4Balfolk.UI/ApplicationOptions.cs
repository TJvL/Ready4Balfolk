using System;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;

namespace Ready4Balfolk.UI;

/// <summary>The few things about a run that are decided before anything is built.</summary>
/// <remarks>
/// These used to be statics on <see cref="Program"/>, read from the command line and then read
/// again from inside the registrations. That made the object graph something only a process start
/// could produce: a scenario run wanting a silent device and its own data directory had no way to
/// ask for either without pretending to be the smoke test.
/// </remarks>
public sealed record ApplicationOptions
{
    /// <summary>Open BASS on its no sound device, for a run with no audio hardware to speak to.</summary>
    public bool UseNoSoundDevice { get; init; }

    /// <summary>The level below which the file logger writes nothing.</summary>
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Info;

    /// <summary>Anything else a run needs in the container, registered last so it wins.</summary>
    /// <remarks>
    /// For a run that has no desktop to answer a file picker, which is a scenario run and nothing
    /// else. Registering last is what lets it stand in for something already registered.
    /// </remarks>
    public Action<IServiceCollection>? AlsoRegister { get; init; }

    /// <summary>Where the application keeps what it writes, or null for the user's profile.</summary>
    /// <remarks>
    /// A scenario run gives every scenario its own directory, so settings, history, the library
    /// index and the dance list are the real stores on real files and no run can see another's.
    /// </remarks>
    public IApplicationSettingsDirectory? SettingsDirectory { get; init; }
}
