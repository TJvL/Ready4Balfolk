using Avalonia.Input;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The gate into the library, which is a person agreeing to three things.</summary>
public sealed class GettingMusicIntoTheLibrary(HeadlessSession session)
{
    private static readonly string[] TheTwoTracks = ["Salamandre", "La Belle"];

    /// <summary>The DJ answers the one thing nobody could answer for them.</summary>
    /// <remarks>
    /// World: a music directory with one tagged track, and a DJ who has declared nothing about
    /// their tags. The artist and the title are read off the tags; the dance is not, because no tag
    /// field is reliably a dance and the application will not assume one.
    /// Steps: open review, give the track its dance, and answer it.
    /// Sees: the track in the catalogue under all three, where the queue can reach it.
    /// </remarks>
    [Fact]
    public async Task DjApprovesADiscoveredTrackWithArtistTitleAndDance()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");

            application.TypeIntoWithin(row, "review.dance", "Mazurka");
            application.Click(RunningApplication.Within(row, "review.approve"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the track to reach the catalogue");

            var catalogued = application.RowsOf("catalog.tracks").Single();

            Assert.Contains("Mazurka", catalogued, StringComparison.Ordinal);
            Assert.Contains("Naragonia", catalogued, StringComparison.Ordinal);
            Assert.Contains("Salamandre", catalogued, StringComparison.Ordinal);
        });
    }

    /// <summary>A track nobody has given a dance does not get in by being answered anyway.</summary>
    /// <remarks>
    /// World: the same music directory and the same untouched tags.
    /// Steps: answer the track with no dance on it, look at the catalogue, then give it a dance and
    /// answer it again.
    /// Sees: nothing in the catalogue the first time and the track the second, which is the gate
    /// the whole library rests on.
    /// </remarks>
    [Fact]
    public async Task DjCannotApproveATrackWithNoDance()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");
            application.Click(RunningApplication.Within(row, "review.approve"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.IsShowing("catalog.tracks"),
                "the main screen to come back");

            Assert.Empty(application.RowsOf("catalog.tracks"));

            application.Click("toolbar.review");
            row = application.Row("review.rows", "Salamandre");
            application.TypeIntoWithin(row, "review.dance", "Mazurka");
            application.Click(RunningApplication.Within(row, "review.approve"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the track to reach the catalogue once it has a dance");
        });
    }

    /// <summary>The DJ corrects the dance before answering, and the correction is what lands.</summary>
    /// <remarks>
    /// World: a music directory with one tagged track and nothing declared about the tags.
    /// Steps: type one dance, think better of it, replace it with another, and answer.
    /// Sees: the catalogue carrying the dance that was answered, not the one that was typed first.
    /// </remarks>
    [Fact]
    public async Task DjCorrectsTheDanceBeforeApproving()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");

            application.TypeIntoWithin(row, "review.dance", "Chapelloise");
            application.TypeIntoWithin(row, "review.dance", "Mazurka");
            application.Click(RunningApplication.Within(row, "review.approve"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the track to reach the catalogue");

            var catalogued = application.RowsOf("catalog.tracks").Single();

            Assert.Contains("Mazurka", catalogued, StringComparison.Ordinal);
            Assert.DoesNotContain("Chapelloise", catalogued, StringComparison.Ordinal);
        });
    }

    /// <summary>The DJ answers the wrong dance, takes it back, and answers again.</summary>
    /// <remarks>
    /// World: a music directory with one tagged track and nothing declared about the tags.
    /// Steps: answer the track with the wrong dance, see that the row now offers to take it back
    /// rather than to answer it again, take it back, and answer with the right one.
    /// Sees: the catalogue holding the dance that was answered second. An individual answer is
    /// sticky by design, so without a way back the first, wrong one would have been permanent.
    /// </remarks>
    [Fact]
    public async Task DjTakesBackAnAnswerAndGivesAnotherOne()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");
            application.TypeIntoWithin(row, "review.dance", "Chapelloise");
            application.Click(RunningApplication.Within(row, "review.approve"));

            // The row has been answered, so it stops asking: the button that gave the answer is the
            // one that takes it back.
            Assert.False(RunningApplication.IsShowingWithin(row, "review.approve"));
            application.Click(RunningApplication.Within(row, "review.withdraw"));

            application.TypeIntoWithin(row, "review.dance", "Mazurka");
            application.Click(RunningApplication.Within(row, "review.approve"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the track to reach the catalogue on the second answer");

            var catalogued = application.RowsOf("catalog.tracks").Single();

            Assert.Contains("Mazurka", catalogued, StringComparison.Ordinal);
            Assert.DoesNotContain("Chapelloise", catalogued, StringComparison.Ordinal);
        });
    }

    /// <summary>Undo belongs to the row the caret is in, not to the row the list has selected.</summary>
    /// <remarks>
    /// World: two tagged tracks and nothing declared about the tags.
    /// Steps: answer both, click into the first one's dance box without the selection following,
    /// and press Ctrl+Z.
    /// Sees: the answer taken back off the row the caret is in, and the selected row untouched.
    /// Clicking into a box does not move the selection, so an undo that reached for the selection
    /// would take back an answer nobody was looking at while leaving the one they were.
    /// </remarks>
    [Fact]
    public async Task UndoTakesBackTheAnswerOnTheRowTheCaretIsIn()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "La Belle")
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 2,
                "both tracks to be waiting for a person");

            foreach (var title in TheTwoTracks)
            {
                var waiting = application.Row("review.rows", title);
                application.TypeIntoWithin(waiting, "review.dance", "Mazurka");
                application.Click(RunningApplication.Within(waiting, "review.approve"));
            }

            // The caret goes into the first row's dance box; the selection stays where answering
            // left it, which is the second row.
            application.Click(RunningApplication.Within(
                application.Row("review.rows", "Salamandre"), "review.dance"));
            application.Press(PhysicalKey.Z, RawInputModifiers.Control);

            Assert.True(RunningApplication.IsShowingWithin(
                application.Row("review.rows", "Salamandre"), "review.approve"));
            Assert.True(RunningApplication.IsShowingWithin(
                application.Row("review.rows", "La Belle"), "review.withdraw"));
        });
    }

    /// <summary>The DJ takes back an answer given in a sitting that is long over.</summary>
    /// <remarks>
    /// World: a music directory with one tagged track and nothing declared about the tags.
    /// Steps: answer the track, leave the screen and come back so the queue is rebuilt, see that
    /// the row is gone, then take the answer back from the catalogue and look at review again.
    /// Sees: the track out of the library and waiting for a person once more. The review queue
    /// drops everything already in the library, so the row that gave the answer does not survive a
    /// rebuild: without a way back beside the library, an answer would be permanent from the next
    /// scan onwards.
    /// </remarks>
    [Fact]
    public async Task DjTakesBackAnAnswerAfterTheReviewRowIsGone()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");
            application.TypeIntoWithin(row, "review.dance", "Chapelloise");
            application.Click(RunningApplication.Within(row, "review.approve"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the track to reach the catalogue");

            // Back into review, where the queue is built again from the index. The answered track
            // is in the library, so it is not in the queue and there is no row to press anything on.
            application.Click("toolbar.review");
            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 0,
                "the answered track to be gone from the rebuilt queue");
            application.Click("screen.back");

            application.Click(application.Row("catalog.tracks", "Salamandre"));
            application.RightClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click(application.Offering(UiStrings.TrackCatalog_WithdrawTrack));

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 0,
                "the track to leave the library with its answer");

            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person again");
        });
    }

    /// <summary>A dance the published list has never heard of keeps its track out.</summary>
    /// <remarks>
    /// World: a library whose tags the DJ trusts, holding one track whose dance is a name
    /// BigBalfolkList does not carry, which is what a local name or a new dance looks like.
    /// Steps: start the application and look at the catalogue.
    /// Sees: the track held back rather than in the library under a name that means nothing to
    /// anybody else, and waiting in review instead.
    /// </remarks>
    [Fact]
    public async Task TrackWithADanceOutsideTheListIsHeldBack()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Sirtaki van de Lage Landen", artist: "Duo Absynthe", title: "Het Veld")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the one track whose dance the list carries");

            Assert.DoesNotContain(
                application.RowsOf("catalog.tracks"),
                row => row.Contains("Het Veld", StringComparison.Ordinal));

            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Any(row => row.Contains("Het Veld", StringComparison.Ordinal)),
                "the held back track to be waiting in review");
        });
    }

    /// <summary>The DJ says their own answer is enough, and the track goes in.</summary>
    /// <remarks>
    /// World: the same library and the same unpublished dance, with the DJ having said that a dance
    /// outside the list is allowed, which is the switch that exists for exactly this.
    /// Steps: start the application and look at the catalogue.
    /// Sees: both tracks in the library, the second under the name its DJ gave it.
    /// </remarks>
    [Fact]
    public async Task DjAllowsDancesOutsideTheListAndTheTrackGoesThrough()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Sirtaki van de Lage Landen", artist: "Duo Absynthe", title: "Het Veld")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AllowDancesOutsideTheList = true })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "both tracks to reach the catalogue");

            Assert.Contains(
                application.RowsOf("catalog.tracks"),
                row => row.Contains("Het Veld", StringComparison.Ordinal)
                       && row.Contains("Sirtaki van de Lage Landen", StringComparison.Ordinal));
        });
    }

    /// <summary>The DJ says a tag value is not a dance, and stops being asked about it.</summary>
    /// <remarks>
    /// World: a library whose tags the DJ trusts, with two tracks whose dance tag holds the name of
    /// the ball they were recorded at rather than a dance.
    /// Steps: open review and say that the value is not a dance.
    /// Sees: both tracks losing it at once, because twenty files claiming the same wrong thing are
    /// one decision, and the value not being offered again.
    /// </remarks>
    [Fact]
    public async Task DjSaysAValueIsNotADanceAndIsNotAskedAgain()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Boombal Gent 2019", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Boombal Gent 2019", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 2,
                "both tracks to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");
            application.Click(RunningApplication.Within(row, "review.not-a-dance"));

            // Both rows lose it, not just the one that was answered: twenty files claiming the same
            // wrong thing are one decision about a value.
            await application.WaitUntil(
                () => application.Rows("review.rows")
                    .All(waiting => RunningApplication.Says(RunningApplication.Within(waiting, "review.dance")).Length == 0),
                "the value to be gone from both tracks");

            Assert.Equal(2, application.RowsOf("review.rows").Count);
        });
    }

    /// <summary>The DJ answers every track that says the same wrong thing, once.</summary>
    /// <remarks>
    /// World: a library whose dance tag the DJ trusts, with two tracks whose tag holds the same
    /// spelling the published list does not carry, which is what one evening's tagging habit looks
    /// like across a folder.
    /// Steps: open review, give the first one the name the list does carry, and say to use it for
    /// every track saying the same thing.
    /// Sees: both tracks in the library, because twenty files claiming the same misspelling are one
    /// decision about a dance rather than twenty.
    /// </remarks>
    [Fact]
    public async Task DjApprovesEverythingTheScannerWasSureAbout()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka van Pierre", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Mazurka van Pierre", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 2,
                "both tracks to be waiting on the same unknown name");

            var row = application.Row("review.rows", "Salamandre");
            application.TypeIntoWithin(row, "review.dance", "Mazurka");

            // The names the box offers hang over the row beneath, buttons and all, so they are put
            // away before anything under them is pressed.
            application.Press(PhysicalKey.Escape);

            application.Click(RunningApplication.Within(row, "review.use-for-all"));

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "both tracks to reach the catalogue on one decision");
        });
    }
}
