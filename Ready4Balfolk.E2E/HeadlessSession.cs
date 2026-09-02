using Avalonia.Headless;

namespace Ready4Balfolk.E2E;

/// <summary>The dispatcher every scenario runs on, and the application it builds per scenario.</summary>
/// <remarks>
/// <para>
/// One session for the assembly, a fresh <c>Application</c> and dispatcher per scenario. The
/// per-scenario part matters: the app holds its container, its stores and its windows in statics,
/// and a scenario that inherited the last one's would be reading someone else's evening.
/// </para>
/// <para>
/// Disposed asynchronously, and that is not a style preference. The synchronous
/// <see cref="IDisposable"/> leaves the dispatcher thread running in the foreground, so the test
/// process finishes its last scenario and then never exits, which on a build agent is a job that
/// hangs rather than one that fails.
/// </para>
/// </remarks>
public sealed class HeadlessSession : IAsyncDisposable
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(ScenarioApplication), AvaloniaTestIsolationLevel.PerTest);

    /// <summary>Builds the world, starts the application on it, and runs the steps.</summary>
    public async Task RunAsync(ScenarioWorld world, Func<RunningApplication, Task> steps)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(steps);

        ScenarioApplication.World = world;
        try
        {
            // Typed, and returning a value, on purpose. The session also offers
            // Dispatch(Action, ...), and an async lambda binds to it happily as an async void: the
            // scenario is then abandoned at its first await, every assertion after that runs with
            // nobody listening, and the run goes green having proved nothing. It did.
            async Task<bool> Scenario()
            {
                await using var application = RunningApplication.Start();
                await steps(application);
                return true;
            }

            await _session.Dispatch(Scenario, TestContext.Current.CancellationToken);
        }
        finally
        {
            ScenarioApplication.World = null;
        }
    }

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
