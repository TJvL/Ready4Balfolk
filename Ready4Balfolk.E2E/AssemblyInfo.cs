using Ready4Balfolk.E2E;
using Xunit.Sdk;
using Xunit.v3;

// One session for the assembly. In the runner it only starts a process per scenario; in each of those
// processes it holds the dispatcher, for the one scenario that process runs.
[assembly: AssemblyFixture(typeof(HeadlessSession))]

// Scenarios run beside each other, because each one is a process of its own: what they would have
// fought over, the audio device and the dispatcher, is not shared any more. Capped, because every
// one of them starts a whole application.
[assembly: Parallelization(Mode = ParallelMode.All, MaxThreads = 4)]
