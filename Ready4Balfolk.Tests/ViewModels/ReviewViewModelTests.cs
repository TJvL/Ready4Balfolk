using System.Reactive.Linq;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Discovery;
using Ready4Balfolk.UI.Views.Review;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// What the screen writes down and what it does next, which is the whole of answering two thousand
/// rows without a mouse.
/// </summary>
public sealed class ReviewViewModelTests : IDisposable
{
    private const string Root = "/music";

    private static readonly DateTime Written = new(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc);

    private readonly ILibraryIndex _libraryIndex = Substitute.For<ILibraryIndex>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly List<(string Path, TrackField Field, string Value)> _approved = [];
    private readonly IConfirmationService _confirmations = Substitute.For<IConfirmationService>();
    private readonly IPreviewPlaybackService _preview = Substitute.For<IPreviewPlaybackService>();
    private readonly NavigationService _navigation = new();
    private readonly ReviewViewModel _sut;

    private ApplicationSettings _stored = new ApplicationSettings() with { MusicDirectoryPath = Root };

    public ReviewViewModelTests()
    {
        // More than one, or there is nothing to walk between and the picker cannot be tested.
        var danceList = new DanceList
        {
            Dances =
            [
                TestData.CreateDance("mazurka", names: ["Mazurka"]),
                TestData.CreateDance("bourree-2-temps", names: ["Bourrée 2 temps"]),
                TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"]),
                // Two names for one dance, so what is written down can be told apart from what
                // was typed.
                TestData.CreateDance("scottish", names: ["Scottish", "Schottische"])
            ]
        };
        var danceListStore = Substitute.For<IDanceListStore>();
        danceListStore.Index.Returns(DanceListIndex.Build(danceList));

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_ => _stored);

        _libraryIndex.SnapshotByPathAsync().Returns(_ => Snapshot());
        _libraryIndex.ApprovalsAsync().Returns(_ =>
            new Dictionary<string, IReadOnlyList<TrackApproval>>(StringComparer.Ordinal));
        _libraryIndex.ApproveIndividuallyAsync(
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyCollection<FieldAnswer>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                foreach (var path in call.Arg<IReadOnlyCollection<string>>()!)
                {
                    foreach (var answer in call.Arg<IReadOnlyCollection<FieldAnswer>>()!)
                    {
                        _approved.Add((path, answer.Field, answer.Value));
                    }
                }

                return Task.CompletedTask;
            });

        _libraryIndex.WithdrawIndividualApprovalsAsync(
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var taken = 0;
                foreach (var path in call.Arg<IReadOnlyCollection<string>>()!)
                {
                    taken += _approved.RemoveAll(entry => entry.Path == path);
                }

                return Task.FromResult(taken);
            });

        _preview.WhenPreviewChanged.Returns(Observable.Never<string?>());
        _preview.WhenProgressChanged.Returns(Observable.Never<TimeSpan>());
        _preview.WhenDurationChanged.Returns(Observable.Never<TimeSpan>());

        // Says yes, so a folder answer is tested rather than the dialog: the dialog has its own
        // test below.
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>())
            .Returns(true);

        var trackStoreForDiscovery = Substitute.For<ITrackStore>();
        trackStoreForDiscovery.IsLoading.Returns(Observable.Return(false));
        var discovery = new DiscoveryViewModel(
            settingsStore, _libraryIndex, danceListStore, trackStoreForDiscovery, Substitute.For<ILoggerService>());

        _sut = new ReviewViewModel(
            _libraryIndex, danceListStore, settingsStore, _trackStore, _preview,
            Substitute.For<INotificationService>(), _confirmations, discovery, _navigation,
            Substitute.For<ILoggerService>());
    }

    [Fact]
    public void LeavingTheScreen_StopsThePreview()
    {
        // A preview left playing must not keep sounding at a screen nobody is looking at it from.
        _navigation.CurrentScreen = Screen.Review;
        _preview.ClearReceivedCalls();

        _navigation.CurrentScreen = Screen.Main;

        _preview.Received().StopAsync();
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task EverythingWaiting_IsARowOfItsOwn()
    {
        await Refresh();

        Assert.Equal(3, _sut.Rows.Count);
    }

    [Fact]
    public async Task TheFirstRowOfAFolder_CarriesItsHeader()
    {
        await Refresh();

        Assert.True(_sut.Rows[0].IsFirstOfGroup);
        Assert.Contains(_sut.Rows, row => !row.IsFirstOfGroup);
    }

    [Fact]
    public async Task AFieldShowsWhereItCameFrom()
    {
        await Refresh();

        Assert.All(_sut.Rows, row => Assert.NotEmpty(row.DanceSource));
    }

    [Fact]
    public async Task AnsweringATrack_WritesAllThreeFields()
    {
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "Mazurka";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";

        await _sut.ApproveCommand.Execute(row);

        Assert.Equal(3, _approved.Count(entry => entry.Path == row.Path));
        Assert.Contains(_approved, entry => entry.Field == TrackField.Dance && entry.Value == "mazurka");
        Assert.True(row.IsApproved);
    }

    [Fact]
    public async Task AnAnswerIsWrittenDownAsTheDanceItMeans()
    {
        // The slug, not the name that was typed. The published list is free to re-spell a dance and
        // to drop a spelling, and an answer stored as a name follows the name rather than the dance.
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "Schottische";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";

        await _sut.ApproveCommand.Execute(row);

        Assert.Contains(_approved, entry => entry.Field == TrackField.Dance && entry.Value == "scottish");
        Assert.True(row.IsApproved);
        Assert.False(row.IsParked);
    }

    [Fact]
    public async Task AnsweringATrack_ShowsItInTheLibraryAtOnce()
    {
        await Refresh();
        await ApproveFirstAsync();

        await _trackStore.Received().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAnsweredRow_StaysWhereItIs()
    {
        // Removing it leaves no way to see what was decided or to fix a mis-click, and makes every
        // row below it jump under the pointer.
        await Refresh();
        var before = _sut.Rows.Count;

        await ApproveFirstAsync();

        Assert.Equal(before, _sut.Rows.Count);
    }

    [Fact]
    public async Task AnsweringATrack_MovesToTheNextOneWaiting()
    {
        await Refresh();
        var first = _sut.Rows[0];

        await ApproveFirstAsync();

        Assert.NotSame(first, _sut.Selected);
        Assert.False(_sut.Selected?.IsApproved);
    }

    [Fact]
    public async Task AnAnsweredRow_SaysSoInAColour()
    {
        // Answered rows stay in the list, so working down a folder has to be visible at a glance.
        await Refresh();
        var row = _sut.Rows[0];

        await ApproveFirstAsync();

        Assert.Equal(ReviewRowState.Answered, row.State);
    }

    [Fact]
    public async Task AParkedRow_IsNotColouredAsDone()
    {
        // Answered and still not in the library. Reading those two the same is how a track goes
        // missing without anybody noticing.
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "Rond de Landéda";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";

        await _sut.ApproveCommand.Execute(row);

        Assert.Equal(ReviewRowState.Parked, row.State);
    }

    [Fact]
    public async Task ADanceTheListDoesNotKnow_IsAnsweredAndParked()
    {
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "Rond de Landéda";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";

        await _sut.ApproveCommand.Execute(row);

        Assert.True(row.IsApproved);
        Assert.True(row.IsParked);
        Assert.Contains(_approved, entry => entry.Value == "Rond de Landéda");
    }

    [Fact]
    public async Task ADanceTheListDoesNotKnow_IsNotParkedWhileTheDoorIsOpen()
    {
        // The same answer as the row above, with the switch that says the DJ's own answer is
        // enough. The track is in the library, and telling them it is parked sends them off to
        // propose a dance at BigBalfolkList they did not need to propose.
        _stored = _stored with { AllowDancesOutsideTheList = true };
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "Rond de Landéda";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";

        await _sut.ApproveCommand.Execute(row);

        Assert.True(row.IsApproved);
        Assert.False(row.IsParked);
        Assert.Equal(ReviewRowState.Answered, row.State);
    }

    [Fact]
    public async Task AFolderAnsweredWithDancesOutsideTheList_ReadsAsAnsweredWhileTheDoorIsOpen()
    {
        // A folder of one evening's tagging habit is where a wrong colour costs the most: every
        // row of it would sit in amber over tracks that all went in.
        _stored = _stored with { AllowDancesOutsideTheList = true };
        await Refresh();
        var row = _sut.Rows.First(candidate => candidate.Folder == "Naragonia");
        foreach (var sibling in _sut.Rows.Where(candidate => candidate.Folder == "Naragonia"))
        {
            sibling.Dance = "Rond de Landéda";
            sibling.Artist = "Naragonia";
            sibling.Title = "Something";
        }

        await _sut.ApproveFolderCommand.Execute(row);

        Assert.All(
            _sut.Rows.Where(candidate => candidate.Folder == "Naragonia"),
            candidate => Assert.Equal(ReviewRowState.Answered, candidate.State));
    }

    [Fact]
    public async Task AnsweringAFolder_TakesEveryTrackInIt()
    {
        await Refresh();
        var row = _sut.Rows.First(candidate => candidate.Folder == "Naragonia");
        foreach (var sibling in _sut.Rows.Where(candidate => candidate.Folder == "Naragonia"))
        {
            sibling.Dance = "Mazurka";
            sibling.Artist = "Naragonia";
            sibling.Title = "Something";
        }

        await _sut.ApproveFolderCommand.Execute(row);

        Assert.All(
            _sut.Rows.Where(candidate => candidate.Folder == "Naragonia"),
            candidate => Assert.True(candidate.IsApproved));
        Assert.DoesNotContain(_sut.Rows.Where(candidate => candidate.Folder != "Naragonia"), candidate => candidate.IsApproved);
    }

    [Fact]
    public async Task ARowMissingAField_IsNotAnsweredWithABlank()
    {
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = string.Empty;

        await _sut.ApproveCommand.Execute(row);

        Assert.False(row.IsApproved);
        Assert.Empty(_approved);
    }

    [Fact]
    public async Task OneDanceCanSettleEveryTrackClaimingTheSameThing()
    {
        await Refresh();
        var row = _sut.Rows.First(candidate => candidate.IsShared);
        row.Dance = "Mazurka";

        await _sut.UseDanceForAllCommand.Execute(row);

        // The dance and nothing else: an artist and a title are per track, so they still want
        // confirming one at a time.
        Assert.Equal(2, _approved.Count(entry => entry.Field == TrackField.Dance && entry.Value == "mazurka"));
        Assert.DoesNotContain(_approved, entry => entry.Field == TrackField.Artist);
        Assert.All(_sut.Rows.Where(candidate => candidate.IsShared), candidate => Assert.False(candidate.IsApproved));

        // Whatever those answers completed belongs in the library now, not the next time some
        // other row happens to be answered.
        await _trackStore.Received().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AValueSaidToBeJunk_IsRememberedAndCleared()
    {
        await Refresh();
        var row = _sut.Rows.First(candidate => candidate.HasUnknownValue);

        await _sut.NotADanceCommand.Execute(row);

        await _libraryIndex.Received().IgnoreValueAsync(row.UnknownValue, Arg.Any<CancellationToken>());
        Assert.All(
            _sut.Rows.Where(candidate => candidate.UnknownValue == row.UnknownValue),
            candidate => Assert.Empty(candidate.Dance));
    }

    [Fact]
    public async Task ARowParkedInAnEarlierSitting_ComesBackWearingIt()
    {
        // It is read off the review rather than remembered, or a track parked on a dance the list
        // does not carry looks untouched every time the screen is opened.
        _libraryIndex.ApprovalsAsync(Arg.Any<CancellationToken>()).Returns(_ => Parked([1]));

        await Refresh();

        var row = _sut.Rows.Single(candidate => candidate.Path == "/music/Naragonia/a.mp3");
        Assert.Equal(ReviewRowState.Parked, row.State);
        Assert.True(row.IsParked);
    }

    [Fact]
    public async Task LettingDancesOutsideTheListIn_EmptiesTheQueueOfThem()
    {
        // The escape hatch: the same track, the same answer, and the only thing that changed is
        // what the gate is willing to let through.
        _libraryIndex.ApprovalsAsync(Arg.Any<CancellationToken>()).Returns(_ => Parked([1]));

        await Refresh();
        Assert.Contains(_sut.Rows, row => row.Path == "/music/Naragonia/a.mp3");

        _stored = _stored with { AllowDancesOutsideTheList = true };
        await Refresh();

        Assert.DoesNotContain(_sut.Rows, row => row.Path == "/music/Naragonia/a.mp3");
    }

    /// <summary>A track answered on every field, with a dance the list has never heard of.</summary>
    private static Dictionary<string, IReadOnlyList<TrackApproval>> Parked(byte[] hash) =>
        new(StringComparer.Ordinal)
        {
            [LibraryKey.For(hash)] =
            [
                Approval(hash, TrackField.Dance, "Rond de Landéda"),
                Approval(hash, TrackField.Artist, "Naragonia"),
                Approval(hash, TrackField.Title, "Le badaud")
            ]
        };

    private static TrackApproval Approval(byte[] hash, TrackField field, string value) => new()
    {
        ContentHash = hash,
        Field = field,
        Value = value,
        Kind = ApprovalKind.Individual,
        FileWriteUtc = Written
    };

    [Fact]
    public async Task ATrackLyingLooseInTheMusicFolder_HasNoFolderToAnswer()
    {
        // Somebody filed the others by putting them in a directory; these were filed nowhere, and
        // treating that as a folder makes the button mean "answer everything I never sorted".
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>()).Returns(_ => new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            ["/music/a.mp3"] = Entry("/music/a.mp3", [1]),
            ["/music/b.mp3"] = Entry("/music/b.mp3", [2])
        });

        await Refresh();

        Assert.All(_sut.Rows, row => Assert.False(row.CanAnswerFolder));

        await _sut.ApproveFolderCommand.Execute(_sut.Rows[0]);

        Assert.Empty(_approved);
    }

    [Fact]
    public async Task ALargeFolderIsConfirmedBeforeItIsAnswered()
    {
        // A keystroke that answers two thousand tracks without saying so is not a bulk confirm.
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Range(0, 40).ToDictionary(
            i => $"/music/Big/{i}.mp3",
            i => Entry($"/music/Big/{i}.mp3", [(byte)i]),
            StringComparer.Ordinal));

        await Refresh();
        await _sut.ApproveFolderCommand.Execute(_sut.Rows[0]);

        await _confirmations.Received().ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>());
        Assert.Empty(_approved);
    }

    [Fact]
    public async Task ALargeFolderIsAskedAboutWithoutMakingYesHardToGive()
    {
        // Approving takes nothing away, so this is the question where return may still say yes.
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Range(0, 40).ToDictionary(
            i => $"/music/Big/{i}.mp3",
            i => Entry($"/music/Big/{i}.mp3", [(byte)i]),
            StringComparer.Ordinal));

        await Refresh();
        await _sut.ApproveFolderCommand.Execute(_sut.Rows[0]);

        await _confirmations.Received().ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            ConfirmationStakes.Reversible, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheFolderButtonSaysHowManyItWouldTake()
    {
        await Refresh();

        var row = _sut.Rows.First(candidate => candidate.Folder == "Naragonia");
        Assert.Equal(2, row.AnswerableInFolder);
        Assert.Contains("2", row.AnswerFolderText);
    }

    [Fact]
    public async Task TypingOffersTheNamesTheListHolds()
    {
        await Refresh();
        var row = _sut.Rows[0];

        row.Dance = "maz";

        Assert.True(row.IsPickerOpen);
        Assert.Contains(row.DanceMatches, match => match.Name == "Mazurka");
        Assert.Equal("Mazurka", row.HighlightedDance);
    }

    [Fact]
    public async Task TheArrowsWalkTheOfferedNamesAndWrap()
    {
        // The reason this list is ours: an AutoCompleteBox takes the first match and closes, which
        // is no use for choosing between the four bourrées it just offered.
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "bourree";

        var first = row.HighlightedDance;
        row.MoveHighlight(1);
        Assert.NotEqual(first, row.HighlightedDance);

        row.MoveHighlight(-1);
        Assert.Equal(first, row.HighlightedDance);
    }

    [Fact]
    public async Task TakingAHighlightedNameWritesItAndClosesTheList()
    {
        await Refresh();
        var row = _sut.Rows[0];
        row.Dance = "maz";

        Assert.True(row.TakeHighlighted());

        Assert.Equal("Mazurka", row.Dance);
        Assert.False(row.IsPickerOpen);
    }

    [Fact]
    public async Task AnExactNameOffersNothingToChooseBetween()
    {
        // Typing the whole name is the answer; a list of one repeating it back is noise.
        await Refresh();
        var row = _sut.Rows[0];

        row.Dance = "Mazurka";

        Assert.False(row.IsPickerOpen);
    }

    [Fact]
    public async Task AFolderWithNothingLeftToTake_StillPointsAtWhatIsHoldingItUp()
    {
        // Asking a second time has to point at the same rows as the first, not at whichever one the
        // keys happen to be on by then.
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
            {
                ["/music/Band/a.mp3"] = Entry("/music/Band/a.mp3", [1]),
                ["/music/Band/b.mp3"] = Entry("/music/Band/b.mp3", [2]) with { Artist = null }
            });

        await Refresh();

        var complete = _sut.Rows.Single(row => row.FileName == "a.mp3");
        var missing = _sut.Rows.Single(row => row.FileName == "b.mp3");

        await _sut.ApproveFolderCommand.Execute(complete);
        Assert.True(complete.IsApproved);

        // Nothing left to take, and the incomplete row is still the reason why.
        var before = missing.RejectedCount;
        await _sut.ApproveFolderCommand.Execute(complete);

        Assert.Equal(before + 1, missing.RejectedCount);
    }

    [Fact]
    public async Task AnAnswerCanBeTakenBack()
    {
        // A dance typed wrong is otherwise permanent: nothing overwrites an individual approval, by
        // design, so the only way out of one has to be a person asking for it.
        await Refresh();
        var row = _sut.Rows[0];
        await ApproveFirstAsync();

        await _sut.WithdrawCommand.Execute(row);

        await _libraryIndex.Received().WithdrawIndividualApprovalsAsync(
            Arg.Is<IReadOnlyCollection<string>>(paths => paths.Contains(row.Path)), Arg.Any<CancellationToken>());
        Assert.False(row.IsApproved);
        Assert.False(row.IsParked);
        Assert.Equal(ReviewRowState.Waiting, row.State);
        Assert.Equal(row.ReasonText, row.StatusText);
    }

    [Fact]
    public async Task TakingAnAnswerBack_PutsTheTrackOutOfTheLibraryAtOnce()
    {
        // It is a question again, so nothing may draw it. Waiting for some other row to be answered
        // would leave it playable on an answer nobody stands behind any more.
        await Refresh();
        var row = _sut.Rows[0];
        await ApproveFirstAsync();
        _trackStore.ClearReceivedCalls();

        await _sut.WithdrawCommand.Execute(row);

        await _trackStore.Received().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnAnswerTakenBack_IsGivenAgainWithTheCorrectionOnIt()
    {
        // The whole point of taking one back: the row opens, the correction is typed, and what is
        // written down is the corrected answer rather than the one that was mistyped.
        await Refresh();
        var row = _sut.Rows[0];
        await ApproveFirstAsync();

        await _sut.WithdrawCommand.Execute(row);
        row.Dance = "Schottische";
        await _sut.ApproveCommand.Execute(row);

        Assert.True(row.IsApproved);
        var dance = Assert.Single(_approved, entry => entry.Path == row.Path && entry.Field == TrackField.Dance);
        Assert.Equal("scottish", dance.Value);
    }

    [Fact]
    public async Task TakingAnAnswerBack_LetsTheFolderCountItAgain()
    {
        // It is one of the folder's questions again, so the button that answers the folder has to
        // say so: a count that still reads nought is a folder that cannot be answered in one act.
        await Refresh();
        var row = _sut.Rows.First(candidate => candidate.Folder == "Naragonia");
        await _sut.ApproveFolderCommand.Execute(row);
        Assert.Equal(0, row.AnswerableInFolder);

        await _sut.WithdrawCommand.Execute(row);

        Assert.Equal(1, row.AnswerableInFolder);
    }

    [Fact]
    public async Task AnsweringAnAnsweredRowAgain_WritesNothingAndSaysSo()
    {
        // Enter held down, or a second click. An answered row has nothing new to say, so it refuses
        // visibly rather than writing the same answer again and rebuilding the library behind it.
        await Refresh();
        var row = _sut.Rows[0];
        await ApproveFirstAsync();
        var written = _approved.Count;
        _trackStore.ClearReceivedCalls();

        await _sut.ApproveCommand.Execute(row);

        Assert.Equal(written, _approved.Count);
        Assert.Equal(1, row.RejectedCount);
        await _trackStore.DidNotReceive().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TakingAnAnswerBackOnAParkedRow_LeavesItParked()
    {
        // A track answered in an earlier sitting, on a dance the published list does not carry,
        // comes back through the queue parked. Park is read off the track's own review, so a row
        // taken back has to land where a rebuild would draw it: landing on waiting makes the row
        // claim to be a question the queue does not think is one, and the keys then stop on it and
        // the folder count offers to answer it.
        _libraryIndex.ApprovalsAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            new Dictionary<string, IReadOnlyList<TrackApproval>>(StringComparer.Ordinal)
            {
                [LibraryKey.For([1])] =
                [
                    Answered([1], TrackField.Dance, "Rond de Landéda"),
                    Answered([1], TrackField.Artist, "Naragonia"),
                    Answered([1], TrackField.Title, "Something")
                ]
            });
        await Refresh();
        var row = _sut.Rows.First(candidate => candidate.State is ReviewRowState.Parked);

        await _sut.ApproveCommand.Execute(row);
        await _sut.WithdrawCommand.Execute(row);

        Assert.True(row.IsParked);
        Assert.Equal(ReviewRowState.Parked, row.State);
    }

    [Fact]
    public async Task AnsweringTheLastQuestion_LeavesTheKeysOnTheRowJustAnswered()
    {
        // Every key on this screen works on the selected row, so selecting nothing once the queue
        // runs out takes the keyboard off the one row an answer could still be taken back from.
        await Refresh();

        foreach (var row in _sut.Rows.ToList())
        {
            row.Dance = "Mazurka";
            row.Artist = "Naragonia";
            row.Title = "Le badaud";
            await _sut.ApproveCommand.Execute(row);
        }

        Assert.Same(_sut.Rows[^1], _sut.Selected);
    }

    [Fact]
    public async Task TakingBackARowNobodyAnswered_DoesNothing()
    {
        await Refresh();

        await _sut.WithdrawCommand.Execute(_sut.Rows[0]);

        await _libraryIndex.DidNotReceive().WithdrawIndividualApprovalsAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    private async Task Refresh() => await _sut.RefreshCommand.Execute();

    private async Task ApproveFirstAsync()
    {
        var row = _sut.Rows[0];
        row.Dance = "Mazurka";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";
        await _sut.ApproveCommand.Execute(row);
    }

    private static Dictionary<string, LibraryEntry> Snapshot() =>
        new(StringComparer.Ordinal)
        {
            ["/music/Naragonia/a.mp3"] = Entry("/music/Naragonia/a.mp3", [1], "Scottiche"),
            ["/music/Naragonia/b.mp3"] = Entry("/music/Naragonia/b.mp3", [2], "Scottiche"),
            ["/music/TREF/c.mp3"] = Entry("/music/TREF/c.mp3", [3])
        };

    private static TrackApproval Answered(byte[] hash, TrackField field, string value) => new()
    {
        ContentHash = hash,
        Field = field,
        Value = value,
        Kind = ApprovalKind.Individual,
        FileWriteUtc = Written
    };

    private static LibraryEntry Entry(string path, byte[] hash, string? unknownDance = null) => new()
    {
        ContentHash = hash,
        Path = path,
        FileSize = 1,
        LastWriteUtc = Written,
        Duration = TimeSpan.FromMinutes(3),
        Format = AudioFormat.Mp3,
        DanceSlug = unknownDance is null ? "mazurka" : null,
        OriginalDance = unknownDance ?? "Mazurka",
        Artist = "Naragonia",
        Title = "Something"
    };
}
