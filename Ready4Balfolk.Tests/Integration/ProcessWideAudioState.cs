namespace Ready4Balfolk.Tests.Integration;

/// <summary>The test classes that touch audio state belonging to the whole process.</summary>
/// <remarks>
/// Two things here are one per process rather than one per test: the set of extensions the library
/// treats as audio, and BASS itself, which is brought up and taken down again by every real
/// playback service that is built. Test classes run in parallel by default, so a class that sets
/// the extensions it wants and a class that brings BASS up with the ones its plugins report were
/// free to overwrite each other halfway through either one. Named here so they take turns.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ProcessWideAudioState
{
    public const string Name = "Process-wide audio state";
}
