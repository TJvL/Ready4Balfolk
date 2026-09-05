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
    public async Task NothingChanged_WritesNothingAndRebuildsNothing()
    {
        var track = TestData.CreateTrack();

        await _sut.ApplyAsync(track, track.Dance, track.Artist, track.Title);

        await _libraryIndex.DidNotReceiveWithAnyArgs()
            .ApproveIndividuallyAsync(default!, default!, TestContext.Current.CancellationToken);
        await _trackStore.DidNotReceive().RefreshLibraryAsync(Arg.Any<CancellationToken>());
    }
}
