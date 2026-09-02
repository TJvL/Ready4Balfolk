using System.Diagnostics;
using Avalonia.Headless;

namespace Ready4Balfolk.E2E;

/// <summary>Runs one scenario, in a process of its own.</summary>
/// <remarks>
/// <para>
/// A scenario starts the whole application, and an application expects to be the only one its
/// process will ever have. Several in a row shared BASS, ReactiveUI's registrations and its
/// schedulers, and each of those broke differently: a playback panel that hid itself, confirmation
/// dialogs that could not report their answer, and a window that stopped following its own view
/// models while the view models were right. None of that is true of the shipped application, which
/// is built once and then runs, so none of it is worth a scenario's time.
/// </para>
/// <para>
/// So a scenario starts this test assembly again for that one test. The scenario method runs twice:
/// in the parent, where it does nothing but wait for its own child and repeat what it said, and in
/// the child, where it is the only scenario in the process and everything it touches is its own.
/// </para>
/// </remarks>
public sealed class HeadlessSession : IAsyncDisposable
{
    /// <summary>Set on the child, so it runs the scenario rather than starting another process.</summary>
    private const string InsideTheChild = "READY4BALFOLK_SCENARIO_CHILD";

    /// <summary>The runner's own talk, which the parent has already said itself.</summary>
    private static readonly string[] Noise =
        ["xUnit.net", "Discovering:", "Discovered:", "Starting:", "Finished:", "=== TEST", "Ready4Balfolk.E2E  Total"];

    /// <summary>Long enough for the slowest scenario, short enough to end a stuck one.</summary>
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Started on the child only, which is what the laziness is for: the parent never needs a
    /// dispatcher, and starting one there would leave a thread with nothing to do in every process.
    /// </summary>
    /// <remarks>
    /// Disposed asynchronously, and that is not a style preference. The synchronous
    /// <see cref="IDisposable"/> leaves the dispatcher thread running in the foreground, so the
    /// process finishes its scenario and then never exits, which on a build agent is a job that
    /// hangs rather than one that fails.
    /// </remarks>
    private readonly Lazy<HeadlessUnitTestSession> _session = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(ScenarioApplication), AvaloniaTestIsolationLevel.PerTest));

    /// <summary>Builds the world, starts the application on it, and runs the steps.</summary>
    public async Task RunAsync(ScenarioWorld world, Func<RunningApplication, Task> steps)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(steps);

        if (Environment.GetEnvironmentVariable(InsideTheChild) is null)
        {
            await RunInAProcessOfItsOwnAsync();
            return;
        }

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

            await _session.Value.Dispatch(Scenario, TestContext.Current.CancellationToken);
        }
        finally
        {
            ScenarioApplication.World = null;
        }
    }

    public ValueTask DisposeAsync() =>
        _session.IsValueCreated ? _session.Value.DisposeAsync() : ValueTask.CompletedTask;

    /// <summary>Starts this assembly again, for the one scenario that is running.</summary>
    private static async Task RunInAProcessOfItsOwnAsync()
    {
        var scenario = TestContext.Current.Test?.TestDisplayName
                       ?? throw new InvalidOperationException("No scenario is running.");

        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add(scenario);
        start.Environment[InsideTheChild] = "1";

        using var child = Process.Start(start)
                          ?? throw new InvalidOperationException($"Could not start a process for {scenario}.");

        var said = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var complained = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        using var patience = new CancellationTokenSource(PatienceLimit);
        try
        {
            await child.WaitForExitAsync(patience.Token);
        }
        catch (OperationCanceledException)
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"{scenario} was still running after {PatienceLimit.TotalMinutes:0} minutes.");
        }

        if (child.ExitCode != 0)
        {
            Assert.Fail($"{WhatItSaid(await said)}{Environment.NewLine}{await complained}".Trim());
        }
    }

    /// <summary>What the child said, without the runner's banner and its summary.</summary>
    private static string WhatItSaid(string output) =>
        string.Join(
            Environment.NewLine,
            output.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => !Noise.Any(start => line.TrimStart().StartsWith(start, StringComparison.Ordinal))))
            .Trim();
}
