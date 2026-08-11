using NSubstitute;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Tests.Integration;

public sealed class SqliteLibraryIndexTests : IAsyncLifetime
{
    private readonly DirectoryInfo _tempDir;
    private readonly SqliteLibraryIndex _sut;

    public SqliteLibraryIndexTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        _sut = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(_sut.OpenAsync(Token));

    [Fact]
    public void Open_CreatesTheDatabaseFile()
        => Assert.True(File.Exists(Path.Combine(_tempDir.FullName, "library.sqlite")));

    [Fact]
    public async Task Write_ThenSnapshot_RoundTripsEverything()
    {
        var entry = Entry("/music/a.mp3", [1, 2, 3], slug: "mazurka");

        await _sut.WriteAsync([entry], Token);
        var snapshot = await _sut.SnapshotByPathAsync(Token);

        var read = snapshot["/music/a.mp3"];
        Assert.Equal(entry.ContentHash, read.ContentHash);
        Assert.Equal(entry.FileSize, read.FileSize);
        Assert.Equal(entry.LastWriteUtc, read.LastWriteUtc);
        Assert.Equal(entry.Duration, read.Duration);
        Assert.Equal(AudioFormat.Flac, read.Format);
        Assert.Equal("mazurka", read.DanceSlug);
        Assert.Equal("Mazurka", read.OriginalDance);
        Assert.Equal("Artist", read.Artist);
        Assert.Equal("Title", read.Title);
    }

    [Fact]
    public async Task Write_UnresolvedTrack_KeepsTheSlugNull()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1], slug: null)], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);

        Assert.Null(snapshot["/music/a.mp3"].DanceSlug);
    }

    [Fact]
    public async Task TheSameAudioInTwoPlaces_IsOneTrackKnownAtBothPaths()
    {
        // Real libraries hold the same album twice, loose and in a folder. Both copies have to be
        // known, or the forgotten one is read from disk on every single startup; and one decision
        // about the recording covers both, because it is the same recording.
        await _sut.WriteAsync([Entry("/music/loose.mp3", [9, 9], slug: "plinn")], Token);
        await _sut.WriteAsync([Entry("/music/album/track.mp3", [9, 9], slug: "plinn")], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("plinn", snapshot["/music/loose.mp3"].DanceSlug);
        Assert.Equal("plinn", snapshot["/music/album/track.mp3"].DanceSlug);
    }

    [Fact]
    public async Task ARenamedFile_KeepsWhatWasDecidedAboutIt()
    {
        await _sut.WriteAsync([Entry("/music/old.mp3", [9, 9], slug: "plinn")], Token);

        await _sut.WriteAsync([Entry("/music/new.mp3", [9, 9], slug: "plinn")], Token);
        await _sut.DeleteMissingAsync(["/music/new.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Single(snapshot);
        Assert.Equal("plinn", snapshot["/music/new.mp3"].DanceSlug);
    }

    [Fact]
    public async Task DifferentAudio_GetsItsOwnRow()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);

        Assert.Equal(2, (await _sut.SnapshotByPathAsync(Token)).Count);
    }

    [Fact]
    public async Task DeleteMissing_ForgetsWhatIsNoLongerThere()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);

        await _sut.DeleteMissingAsync(["/music/a.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Single(snapshot);
        Assert.True(snapshot.ContainsKey("/music/a.mp3"));
    }

    [Fact]
    public async Task DeleteMissing_WithNothingPresent_EmptiesTheIndex()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.DeleteMissingAsync([], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

    [Fact]
    public async Task Write_WithNothingToWrite_DoesNotThrow()
    {
        await _sut.WriteAsync([], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

    [Fact]
    public async Task UseBeforeOpen_Throws()
    {
        using var unopened = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => unopened.SnapshotByPathAsync(Token));
    }

    public ValueTask DisposeAsync()
    {
        _sut.Dispose();

        try
        {
            if (_tempDir.Exists)
            {
                _tempDir.Delete(true);
            }
        }
        catch (IOException)
        {
            // Best effort. A temporary directory that will not delete is the operating system
            // holding a handle a moment longer, not a test that failed, and reporting it as one
            // hides whatever the test was actually about.
        }

        return ValueTask.CompletedTask;
    }

    private IApplicationSettingsDirectory DirectoryPointingAtTemp()
    {
        var directory = Substitute.For<IApplicationSettingsDirectory>();
        directory.DirectoryInfoRoot.Returns(_ => _tempDir);
        return directory;
    }

    [Fact]
    public async Task AnApproval_SurvivesTheScanThatRewritesTheTrack()
    {
        // The bug this whole table exists for: the row is upserted on every rescan, and the answer a
        // person gave used to be one of the columns it overwrote.
        await _sut.WriteAsync([Entry("/music/a.mp3", [1], slug: "mazurka")], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Dance, "scottish", Token);

        await _sut.WriteAsync([Entry("/music/a.mp3", [1], slug: "waltz")], Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal("scottish", approval.Value);
        Assert.Equal(ApprovalKind.Individual, approval.Kind);
    }

    [Fact]
    public async Task AnApproval_FollowsTheAudioThroughARename()
    {
        // Same audio, new path. The content hash is what the answer hangs on, so nobody is asked
        // again because a file was renamed or retagged.
        await _sut.WriteAsync([Entry("/music/a.mp3", [7])], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Artist, "Naragonia", Token);

        await _sut.WriteAsync([Entry("/music/renamed.mp3", [7])], Token);

        var approvals = await _sut.ApprovalsAsync(Token);
        Assert.Equal("Naragonia", Assert.Single(approvals[LibraryKey.For([7])]).Value);
    }

    [Fact]
    public async Task ApprovingAField_ReplacesWhateverWasAgreedToBefore()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Title, "First", Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Title, "Second", Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal("Second", approval.Value);
    }

    [Fact]
    public async Task AnApprovalLandsOnTheAudio_SoBothCopiesOfATrackGetIt()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [9]), Entry("/music/compilation/a.mp3", [9])], Token);

        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Dance, "mazurka", Token);

        Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([9])]);
    }

    [Fact]
    public async Task RevokingRules_LeavesWhatAPersonAnsweredThemselves()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);
        await _sut.ApproveAsync([ByRule([1], TrackField.Artist, "Naragonia")], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Title, "Le badaud", Token);

        await _sut.RevokeRuleApprovalsAsync(Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal(TrackField.Title, approval.Field);
    }

    [Fact]
    public async Task AByRuleApproval_RemembersWhichRuleDidIt()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.ApproveAsync([ByRule([1], TrackField.Artist, "Naragonia")], Token);

        Assert.Equal("%d - %a - %t", Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]).Rule);
    }

    [Fact]
    public async Task AudioNothingPointsAtAnyMore_TakesItsApprovalsWithIt()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], TrackField.Dance, "mazurka", Token);

        await _sut.DeleteMissingAsync([], Token);

        Assert.Empty(await _sut.ApprovalsAsync(Token));
    }

    [Fact]
    public async Task InReview_CountsWhatIsNotAnsweredOnEveryField()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);
        foreach (var field in new[] { TrackField.Dance, TrackField.Artist, TrackField.Title })
        {
            await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], field, "answered", Token);
        }

        Assert.Equal(1, await _sut.CountInReviewAsync(Token));
    }

    private static TrackApproval ByRule(byte[] hash, TrackField field, string value) => new()
    {
        ContentHash = hash,
        Field = field,
        Value = value,
        Kind = ApprovalKind.ByRule,
        Rule = "%d - %a - %t",
        FileWriteUtc = new DateTime(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc)
    };

    private static LibraryEntry Entry(string path, byte[] hash, string? slug = "mazurka")
        => new()
        {
            ContentHash = hash,
            Path = path,
            FileSize = 1234,
            LastWriteUtc = new DateTime(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromSeconds(180),
            Format = AudioFormat.Flac,
            DanceSlug = slug,
            OriginalDance = "Mazurka",
            Artist = "Artist",
            Title = "Title"
        };
}
