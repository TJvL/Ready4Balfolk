using System.Globalization;
using System.Reflection;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// What a first run has against what the wizard tells the user it has. They were not the same: the
/// step promised a copy that no project has ever embedded, on the one screen somebody setting up in
/// a hall reads, so the promise was believed until nothing could be answered.
/// </summary>
public sealed class FirstRunDanceListPromiseTests
{
    [Fact]
    public void NoDanceListShipsWithTheBuildForAFirstRunToFallBackOn()
    {
        var assemblies = new[] { typeof(DanceListStore).Assembly, typeof(UiStrings).Assembly };

        // Both ways a list could ship: baked into an assembly, or dropped beside the binaries. What
        // decides is whether the bytes read as a dance list, not what the file is called, because a
        // list under any other name would still be a fallback the wizard could promise.
        var embedded =
            from assembly in assemblies
            from name in assembly.GetManifestResourceNames()
            where ReadsAsADanceList(ReadResource(assembly, name))
            select name;

        var alongside =
            from directory in assemblies
                .Select(assembly => Path.GetDirectoryName(assembly.Location)!)
                .Distinct(StringComparer.Ordinal)
            from path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            where ReadsAsADanceList(File.ReadAllText(path))
            select path;

        Assert.Empty(embedded.Concat(alongside));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("nl")]
    public void TheWizardTellsAFirstRunHowToGetOne(string language)
    {
        var detail = UiStrings.ResourceManager.GetString(
            "Wizard_DanceList_Detail", CultureInfo.GetCultureInfo(language));

        Assert.NotNull(detail);

        // Fetching and importing are the only two ways a list ever arrives, so the text beside
        // those two buttons has to name both of them: where it comes from, and what a file
        // carried in on a stick is called.
        Assert.Contains("BigBalfolkList", detail, StringComparison.Ordinal);
        Assert.Contains("dances.json", detail, StringComparison.Ordinal);
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool ReadsAsADanceList(string content)
    {
        try
        {
            DanceListReader.Read(content);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
