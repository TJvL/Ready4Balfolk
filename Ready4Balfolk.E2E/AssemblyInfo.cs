using Ready4Balfolk.E2E;
using Xunit.Sdk;
using Xunit.v3;

// One dispatcher for the assembly, handed out one scenario at a time.
[assembly: AssemblyFixture(typeof(HeadlessSession))]

// Scenarios run one after another. They share a dispatcher thread, they open an audio device, and
// the ones that switch the web server on bind a port; two at once would be two evenings in one room.
[assembly: Parallelization(Mode = ParallelMode.None)]
