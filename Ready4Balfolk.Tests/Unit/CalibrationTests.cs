using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Calibration is judged on what it refuses to say. Naming a field it cannot identify is worse than
/// saying nothing, because the user is asked to greenlight it across their whole library.
/// </summary>
public sealed class CalibrationTests
{
    private readonly DanceListIndex _dances = DanceListIndex.Build(new DanceList
    {
        Dances =
        [
            TestData.CreateDance("mazurka", names: ["Mazurka"]),
            TestData.CreateDance("scottish", names: ["Scottish"]),
            TestData.CreateDance("waltz", names: ["Valse"])
        ]
    });

    [Fact]
    public void ALibraryWithNothingInIt_SaysNothing() =>
        Assert.True(Calibration.Measure([], _dances, DiscoverySettings.Undeclared).IsEmpty);

    [Fact]
    public void AShapeTooSmallToMeanAnything_IsNotProposed()
    {
        // Three files that happen to look alike is a coincidence, not a rule worth a person's
        // greenlight over their whole library.
        var report = Calibration.Measure(
            [.. Enumerable.Range(0, 3).Select(i => File($"Mazurka - Naragonia - Tune {i}"))],
            _dances,
            DiscoverySettings.Undeclared);

        Assert.Empty(report.Shapes);
    }

    [Fact]
    public void TheDanceIsIdentifiedByTheListRatherThanByItsPosition()
    {
        var report = Calibration.Measure(Library(), _dances, DiscoverySettings.Undeclared);

        var shape = report.Shapes[0];
        Assert.Equal(TrackField.Dance, shape.Positions[0].Field);
        Assert.Equal(shape.Files, shape.Positions[0].DanceNames);
    }

    [Fact]
    public void APositionNothingCanSpeakFor_IsNamedNothingAndKeepsThePattern()
    {
        // "An Tri dipop - Ar Re Yaouank - Treizhour": a band, a band and a title. Both bands recur
        // across the library as bands do, neither agrees with a tag and neither is a dance, so
        // nothing can say what they are and they come back as %i rather than as a guess.
        var bands = new[] { "An Tri dipop", "Ar Re Yaouank", "Startijenn", "Plantec" };
        var files = Enumerable.Range(0, 12)
            .Select(i => File($"{bands[i % 4]} - {bands[(i + 1) % 4]} - Tune {i}"))
            .ToList();

        var shape = Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Shapes[0];

        Assert.Null(shape.Positions[0].Field);
        Assert.Contains("%i", shape.Pattern);
    }

    [Fact]
    public void AShapeWhereNothingCanBeNamed_IsShownAndNotDeclarable()
    {
        var files = Enumerable.Range(0, 12).Select(i => File($"{i:00} - {i:00}")).ToList();

        var shape = Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Shapes[0];

        Assert.Null(shape.Pattern);
        Assert.NotEmpty(shape.Samples);
    }

    [Fact]
    public void ATagAgreeingSettlesAField()
    {
        // The strongest signal there is: the tag says what the field is, and the position merely
        // turns out to match it.
        var files = Enumerable.Range(0, 12)
            .Select(i => File($"Whoever {i} - Tune {i}") with { TagArtist = $"Whoever {i}" })
            .ToList();

        var shape = Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Shapes[0];

        Assert.Equal(TrackField.Artist, shape.Positions[0].Field);
        Assert.Equal("%a - %t", shape.Pattern);
    }

    [Fact]
    public void ConstantInAFolderAndVaryingBetweenThem_IsAnArtist()
    {
        // What a library with no usable tags still has: how a value moves across files.
        var files = new List<CalibrationFile>();
        foreach (var band in new[] { "Naragonia", "Bal O'Gadjo", "TREF" })
        {
            files.AddRange(Enumerable.Range(0, 5).Select(i =>
                File($"{band} - Tune {band} {i}", [band])));
        }

        var shape = Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Shapes[0];

        Assert.Equal(TrackField.Artist, shape.Positions[0].Field);
        Assert.Equal(TrackField.Title, shape.Positions[1].Field);
    }

    [Fact]
    public void ALevelThatMatchesTheArtistTag_IsProposedAsTheArtist()
    {
        var files = new List<CalibrationFile>();
        foreach (var band in new[] { "Naragonia", "Bal O'Gadjo", "TREF", "Plantec" })
        {
            files.AddRange(Enumerable.Range(0, 4).Select(i =>
                File($"{i:00} - Tune {i}", [band]) with { TagArtist = band }));
        }

        var proposal = Assert.Single(Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Folders);

        Assert.Equal(1, proposal.Level);
        Assert.Equal(FolderRole.Artist, proposal.Role);
        Assert.Equal(proposal.Considered, proposal.Agreeing);
    }

    [Fact]
    public void ALevelTheListRecognises_IsProposedAsTheDance()
    {
        var files = new List<CalibrationFile>();
        foreach (var dance in new[] { "Mazurka", "Scottish", "Valse" })
        {
            files.AddRange(Enumerable.Range(0, 5).Select(i => File($"Tune {dance} {i}", [dance])));
        }

        var proposal = Assert.Single(Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Folders);

        Assert.Equal(FolderRole.Dance, proposal.Role);
    }

    [Fact]
    public void OneValueForTheWholeLibrary_MeansNothing()
    {
        // And so does a different value in every file: both are the same non-answer.
        var flat = Enumerable.Range(0, 12).Select(i => File($"Tune {i}", ["Music"])).ToList();
        var perFile = Enumerable.Range(0, 12).Select(i => File($"Tune {i}", [$"Folder {i}"])).ToList();

        Assert.Empty(Calibration.Measure(flat, _dances, DiscoverySettings.Undeclared).Folders);
        Assert.Empty(Calibration.Measure(perFile, _dances, DiscoverySettings.Undeclared).Folders);
    }

    [Fact]
    public void ALevelTheUserHasAlreadyStated_IsNotProposedAgain()
    {
        var files = new List<CalibrationFile>();
        foreach (var band in new[] { "Naragonia", "Bal O'Gadjo", "TREF", "Plantec" })
        {
            files.AddRange(Enumerable.Range(0, 4).Select(i =>
                File($"{i:00} - Tune {i}", [band]) with { TagArtist = band }));
        }

        var declared = new DiscoverySettings { FolderRoles = [FolderRole.Artist] };

        Assert.Empty(Calibration.Measure(files, _dances, declared).Folders);
    }

    [Fact]
    public void TheSameFieldIsNeverClaimedTwice()
    {
        // Two positions that both look like the title cannot both be it, and a pattern saying so
        // would not compile.
        var files = Enumerable.Range(0, 12).Select(i => File($"Tune {i} - Other {i} - Third {i}")).ToList();

        var shape = Calibration.Measure(files, _dances, DiscoverySettings.Undeclared).Shapes[0];

        Assert.NotNull(shape.Pattern);
        Assert.Equal(1, shape.Pattern!.Split("%t").Length - 1);
    }

    private static IReadOnlyList<CalibrationFile> Library() =>
    [
        .. Enumerable.Range(0, 12).Select(i =>
            File($"{(i % 2 == 0 ? "Mazurka" : "Scottish")} - Naragonia - Tune {i}", ["Naragonia"]))
    ];

    private static CalibrationFile File(string name, IReadOnlyList<string>? folders = null) => new()
    {
        FileName = name,
        Folders = folders ?? []
    };
}
