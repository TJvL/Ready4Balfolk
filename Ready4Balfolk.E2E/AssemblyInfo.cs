using Ready4Balfolk.E2E;
using Xunit.Sdk;
using Xunit.v3;

// One dispatcher for the assembly, handed out one scenario at a time.
[assembly: AssemblyFixture(typeof(HeadlessSession))]

// Scenarios run beside each other, because each one is a process of its own: what they would have
// fought over, the audio device and the dispatcher, is not shared any more. Capped, because every
// one of them starts a whole application.
[assembly: Parallelization(Mode = ParallelMode.All, MaxThreads = 4)]
