using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

public sealed class TrackEditorServiceTests
{
    private readonly ILibraryIndex _libraryIndex = Substitute.For<ILibraryIndex>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly TrackEditorService _sut;

    public TrackEditorServiceTests()
    {
        var danceListStore = Substitute.For<IDanceListStore>();
        danceListStore.Index.Returns(DanceListIndex.Build(TestData.CreateSimpleDanceList()));
        _sut = new TrackEditorService(danceListStore, _libraryIndex, _trackStore);
    }

    [Fact]
    public async Task OnlyTheChangedFields_AreApproved()
    {
        // An untouched field keeps the approval it already had, so one a rule answered is still
        // taken back when that rule changes.
        var track = TestData.CreateTrack();

        await _sut.ApplyAsync(track, "Mazurka", track.Artist, "Corrected");

        await _libraryIndex.Received(1).ApproveIndividuallyAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Is<IReadOnlyCollection<FieldAnswer>>(answers =>
                answers != null && answers.Count == 1 && answers.Contains(new FieldAnswer(TrackField.Title, "Corrected"))),
            Arg.Any<CancellationToken>());
        await _trackStore.Received(1).RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ACorrectedDance_IsWrittenDownAsTheDanceItMeans()
    {
        // The dialog hands over the name the person read, which is what "changed" is decided on.
        // What is written down is the slug: the list may re-spell a dance, and a correction stored
        // as a name follows the name rather than the dance.
        var track = TestData.CreateTrack();

        await _sut.ApplyAsync(track, "Schottische", track.Artist, track.Title);

        await _libraryIndex.Received(1).ApproveIndividuallyAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Is<IReadOnlyCollection<FieldAnswer>>(answers =>
                answers != null && answers.Contains(new FieldAnswer(TrackField.Dance, "scottish"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Withdrawing_TakesTheAnswerBackAndPutsTheTrackOutOfTheLibrary()
    {
        // The lasting way back from an answer. The review queue drops every track already in the
        // library and is rebuilt on every scan, so the row that gave the answer is gone by the next
        // one; the track itself is still here, in the list of what got through the gate.
        var track = TestData.CreateTrack();
        _libraryIndex.WithdrawIndividualApprovalsAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(3);

        Assert.True(await _sut.WithdrawAsync(track));

        await _libraryIndex.Received(1).WithdrawIndividualApprovalsAsync(
            Arg.Is<IReadOnlyCollection<string>>(paths => paths.Contains(track.FileInfo.FullName)),
            Arg.Any<CancellationToken>());
        await _trackStore.Received(1).RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithdrawingATrackNobodyAnswered_SaysSoAndRebuildsNothing()
    {
        // In the library on a rule or on its own tags, so there is nothing of a person's to take
        // back. Rebuilding anyway is a whole library re-read for a delete that removed nothing, and
        // the caller still has to be able to say that the track has not moved.
        var track = TestData.CreateTrack();
        _libraryIndex.WithdrawIndividualApprovalsAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(0);

        Assert.False(await _sut.WithdrawAsync(track));

        await _trackStore.DidNotReceive().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NothingChanged_WritesNothingAndRebuildsNothing()
    {
        var track = TestData.CreateTrack();

        await _sut.ApplyAsync(track, track.Dance, track.Artist, track.Title);

        await _libraryIndex.DidNotReceiveWithAnyArgs()
            .ApproveIndividuallyAsync(default!, default!, TestContext.Current.CancellationToken);
        await _trackStore.DidNotReceive().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }
}
