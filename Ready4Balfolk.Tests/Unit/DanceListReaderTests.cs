using Ready4Balfolk.Domain.Services.Dances;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The reader is the only door the list comes through, from all three sources, so what it refuses
/// is what the application is protected from.
/// </summary>
public sealed class DanceListReaderTests
{
    [Fact]
    public void Read_TheShapeBigBalfolkListPublishes_IsUnderstood()
    {
        const string json = """
            {"formatVersion":4,
             "tags":["bretagne","france"],
             "dances":[{"slug":"an-dro","names":["An dro","En dro"],"tags":["bretagne","france"]}]}
            """;

        var list = DanceListReader.Read(json);

        var dance = Assert.Single(list.Dances);
        Assert.Equal("an-dro", dance.Slug);
        Assert.Equal("An dro", dance.DisplayName);
        Assert.Contains("bretagne", dance.Tags);
    }

    [Fact]
    public void Read_AnOlderFormat_IsRefused()
    {
        // The categories-and-weights shape is gone, and there is no migration from it.
        const string json = """{"formatVersion":2,"categories":[{"name":"Common","dances":[]}]}""";

        var exception = Assert.Throws<InvalidDataException>(() => DanceListReader.Read(json));

        Assert.Contains("2", exception.Message);
    }

    [Fact]
    public void Read_ANewerFormat_IsRefused()
    {
        const string json = """{"formatVersion":5,"dances":[{"slug":"an-dro","names":["An dro"]}]}""";

        Assert.Throws<InvalidDataException>(() => DanceListReader.Read(json));
    }

    [Fact]
    public void Read_NotJson_IsRefused() => Assert.Throws<InvalidDataException>(() => DanceListReader.Read("nope"));

    // A truncated download read as an empty list would leave the application with no vocabulary
    // and no sign that anything had gone wrong.
    [Fact]
    public void Read_NoDances_IsRefused() =>
        Assert.Throws<InvalidDataException>(() => DanceListReader.Read("""{"formatVersion":4,"dances":[]}"""));

    [Fact]
    public void Read_ANameMeaningTwoDances_IsRefusedAndSaysWhich()
    {
        const string json = """
            {"formatVersion":4,
             "dances":[{"slug":"a","names":["Hanter dro"]},{"slug":"b","names":["Hanter-dro"]}]}
            """;

        var exception = Assert.Throws<InvalidDataException>(() => DanceListReader.Read(json));

        Assert.Contains("Hanter-dro", exception.Message);
    }

    [Fact]
    public void Read_ATagNoneOfTheTopLevelTagsDeclares_IsRefused()
    {
        const string json = """
            {"formatVersion":4,"tags":["bretagne"],
             "dances":[{"slug":"an-dro","names":["An dro"],"tags":["bretagne","invented"]}]}
            """;

        Assert.Throws<InvalidDataException>(() => DanceListReader.Read(json));
    }
}
