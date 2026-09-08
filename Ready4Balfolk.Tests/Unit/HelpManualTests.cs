using System.Globalization;
using System.Reflection;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The manual against the screens it describes. A reader looking for a button called Review on a
/// screen whose button says Nakijken is not helped by a manual, and the two languages had drifted
/// apart from the application and from each other.
/// </summary>
public sealed class HelpManualTests
{
    /// <summary>
    /// The screens and buttons the manual names by name, as resource keys rather than as words, so
    /// renaming a label in the application is what fails this rather than somebody noticing. Each
    /// key is paired with the ordinal of the heading (counting every #, ##, ### line from the top,
    /// both languages in lockstep) whose section is where a reader actually looks for that label --
    /// not just anywhere the word happens to occur, which a Phone Remote paragraph or an unrelated
    /// heading could satisfy by accident.
    /// </summary>
    public static TheoryData<string, int> NamedOnScreen() => new()
    {
        { "Toolbar_ExitLabel", 9 },              // ### Exit
        { "Toolbar_ReviewLabel", 12 },            // ### Review (toolbar)
        { "Toolbar_DisplayServed", 13 },          // ### Display and Remote
        { "Toolbar_RemoteServed", 13 },           // ### Display and Remote
        { "QueueToolbar_SwitchToHistory", 25 },   // ### Queue Toolbar
        { "QueueToolbar_QueueRandomTrack", 25 },  // ### Queue Toolbar
        { "QueueToolbar_RequestStop", 25 },       // ### Queue Toolbar
        { "QueueToolbar_RequestDelay", 25 },      // ### Queue Toolbar
        { "QueueToolbar_RequestMessage", 25 },    // ### Queue Toolbar
        { "QueueToolbar_RemoveSelected", 25 },    // ### Queue Toolbar
        { "QueueToolbar_ClearQueue", 25 },        // ### Queue Toolbar
        { "Queue_MoveUp", 25 },                   // ### Queue Toolbar
        { "Queue_MoveDown", 25 },                 // ### Queue Toolbar
        { "HistoryToolbar_SwitchToQueue", 29 },   // ### History Toolbar
        { "HistoryToolbar_ExportHistory", 29 },   // ### History Toolbar
        { "TrackCatalog_SwitchToDanceList", 38 }, // ### Switch to the dance list
        { "TrackCatalog_EditTrack", 36 },         // ### Fixing a typo where you see it
        { "TrackCatalog_WithdrawTrack", 37 },     // ### Taking an answer back (Track Catalog)
        { "Settings_RunSetupAgain", 53 },         // ### Music Directory (Settings)
        { "Discovery_ProposalAccept", 5 },        // ### Rules: telling it how your files are named
        { "Settings_ThemeAuto", 66 },             // ### Theme
        { "Settings_ThemeLight", 66 },            // ### Theme
        { "Settings_ThemeDark", 66 },             // ### Theme
        { "DanceList_Update", 44 },               // ### Keeping it up to date
        { "DanceList_UpdateFromFile", 44 },       // ### Keeping it up to date
        { "DanceList_ClearPool", 41 },            // ### Choosing what random picks from
    };

    [Theory]
    [MemberData(nameof(NamedOnScreen))]
    public void EachManualCallsAThingWhatItsOwnLanguageCallsIt(string key, int headingOrdinal)
    {
        foreach (var language in new[] { "en", "nl" })
        {
            var label = UiStrings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(language));

            Assert.NotNull(label);
            Assert.Contains(label, Section(Manual(language), headingOrdinal), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Names the Dutch application has never shown. Each was in the manual, and each sends a reader
    /// hunting for a button that is not there.
    /// </summary>
    [Theory]
    [InlineData("Review")]
    [InlineData("Exit")]
    [InlineData("Verklaar het")]
    [InlineData("Setup opnieuw uitvoeren")]
    [InlineData("Stop toevoegen")]
    [InlineData("Pauze toevoegen")]
    [InlineData("Bericht toevoegen")]
    public void TheDutchManualDoesNotUseANameTheDutchScreensDoNot(string name)
        => Assert.DoesNotContain(name, Manual("nl"), StringComparison.Ordinal);

    /// <summary>The English equivalents, which had drifted from the tooltips in the same way.</summary>
    [Theory]
    [InlineData("Enqueue Stop")]
    [InlineData("Enqueue Delay")]
    [InlineData("Toggle to History")]
    [InlineData("Toggle to Queue")]
    [InlineData("Toggle to Dance List")]
    public void TheEnglishManualDoesNotUseANameTheEnglishScreensDoNot(string name)
        => Assert.DoesNotContain(name, Manual("en"), StringComparison.Ordinal);

    /// <summary>
    /// One translation of the other, section for section. A paragraph added to one language and not
    /// the other is how the two came to describe different applications.
    /// </summary>
    [Fact]
    public void TheTwoManualsHaveTheSameShape()
        => Assert.Equal(Headings(Manual("en")), Headings(Manual("nl")));

    private static List<string> Headings(string manual) =>
    [
        .. manual.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith('#'))
            .Select(line => new string('#', line.Length - line.TrimStart('#').Length))
    ];

    /// <summary>
    /// The text of one section: from the nth heading in the document (1-based, every #, ##, ###
    /// line counted from the top) up to whichever comes first of the next heading at the same
    /// depth or shallower, or the end of the file. Both manuals share the same heading structure
    /// in the same order, so the same ordinal names the matching section in each. Lines are joined
    /// with a space rather than a newline, so a label that Markdown happens to soft-wrap across two
    /// source lines (a prose width limit, not a meaningful break) still reads as one run of words.
    /// </summary>
    private static string Section(string manual, int headingOrdinal)
    {
        var lines = manual.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var headingIndices = lines
            .Select((line, index) => (line, index))
            .Where(entry => entry.line.StartsWith('#'))
            .Select(entry => entry.index)
            .ToList();

        var start = headingIndices[headingOrdinal - 1];
        var depth = lines[start].TakeWhile(c => c == '#').Count();
        var end = headingIndices.FirstOrDefault(
            index => index > start && lines[index].TakeWhile(c => c == '#').Count() <= depth,
            lines.Count);

        return string.Join(' ', lines.GetRange(start, end - start));
    }

    private static string Manual(string language)
    {
        var name = language == "en" ? "help.md" : $"help.{language}.md";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"{name} is not embedded in the test assembly.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
