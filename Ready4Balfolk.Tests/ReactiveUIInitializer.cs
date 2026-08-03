using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace Ready4Balfolk.Tests;

/// <summary>
/// ReactiveUI 23+ must be initialized through the builder before WhenAnyValue
/// and friends can be used; the app does this via UseReactiveUI, tests do it
/// here once per test assembly. Both schedulers run inline so derived
/// properties propagate synchronously, matching the pre-23 unit-test-runner
/// behavior the assertions rely on.
/// </summary>
internal static class ReactiveUIInitializer
{
    [ModuleInitializer]
    internal static void Initialize() =>
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithMainThreadScheduler(CurrentThreadScheduler.Instance)
            .WithTaskPoolScheduler(CurrentThreadScheduler.Instance)
            .WithCoreServices()
            .BuildApp();
}
