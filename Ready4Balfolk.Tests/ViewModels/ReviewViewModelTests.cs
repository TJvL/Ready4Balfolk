using System.Reactive.Linq;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
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
    private readonly ReviewViewModel _sut;

    public ReviewViewModelTests()
    {
        var danceList = new DanceList { Dances = [TestData.CreateDance("mazurka", names: ["Mazurka"])] };
        var danceListStore = Substitute.For<IDanceListStore>();
        danceListStore.Index.Returns(DanceListIndex.Build(danceList));

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new ApplicationSettings() with { MusicDirectoryPath = Root });

        _libraryIndex.SnapshotByPathAsync().Returns(_ => Snapshot());
        _libraryIndex.ApprovalsAsync().Returns(_ =>
            (IReadOnlyDictionary<string, IReadOnlyList<TrackApproval>>)new Dictionary<string, IReadOnlyList<TrackApproval>>(StringComparer.Ordinal));
        _libraryIndex.ApproveIndividuallyAsync(
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<TrackField>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                foreach (var path in call.Arg<IReadOnlyCollection<string>>()!)
                {
                    _approved.Add((path, call.Arg<TrackField>(), call.Arg<string>()!));
                }

                return Task.CompletedTask;
            });

        _sut = new ReviewViewModel(
            _libraryIndex, danceListStore, settingsStore, _trackStore, Substitute.For<ILoggerService>());
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
        Assert.Contains(_approved, entry => entry.Field == TrackField.Dance && entry.Value == "Mazurka");
        Assert.True(row.IsApproved);
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

    private async Task Refresh() => await _sut.RefreshCommand.Execute();

    private async Task ApproveFirstAsync()
    {
        var row = _sut.Rows[0];
        row.Dance = "Mazurka";
        row.Artist = "Naragonia";
        row.Title = "Le badaud";
        await _sut.ApproveCommand.Execute(row);
    }

    private static IReadOnlyDictionary<string, LibraryEntry> Snapshot() =>
        new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            ["/music/Naragonia/a.mp3"] = Entry("/music/Naragonia/a.mp3", [1]),
            ["/music/Naragonia/b.mp3"] = Entry("/music/Naragonia/b.mp3", [2]),
            ["/music/TREF/c.mp3"] = Entry("/music/TREF/c.mp3", [3])
        };

    private static LibraryEntry Entry(string path, byte[] hash) => new()
    {
        ContentHash = hash,
        Path = path,
        FileSize = 1,
        LastWriteUtc = Written,
        Duration = TimeSpan.FromMinutes(3),
        Format = AudioFormat.Mp3,
        DanceSlug = "mazurka",
        OriginalDance = "Mazurka",
        Artist = "Naragonia",
        Title = "Something"
    };
}
