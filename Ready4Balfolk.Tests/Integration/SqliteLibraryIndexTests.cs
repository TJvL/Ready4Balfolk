using System.IO.Abstractions;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Tests.Integration;

public sealed class SqliteLibraryIndexTests : IAsyncLifetime
{
    private readonly IDirectoryInfo _tempDir;
    private readonly SqliteLibraryIndex _sut;

    public SqliteLibraryIndexTests()
    {
        _tempDir = new FileSystem().DirectoryInfo.New(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
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
        await _sut.DeleteMissingAsync(["/music/new.mp3"], [], Token);

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

        await _sut.DeleteMissingAsync(["/music/a.mp3"], [], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Single(snapshot);
        Assert.True(snapshot.ContainsKey("/music/a.mp3"));
    }

    [Fact]
    public async Task DeleteMissing_WithNothingPresent_EmptiesTheIndex()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.DeleteMissingAsync([], [], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

    /// <summary>
    /// What the user was asked about and said to keep. The row and everything approved about it
    /// stay; only its reach is gone.
    /// </summary>
    [Fact]
    public async Task DeleteMissing_WhatIsToBeKept_StaysAsUnavailable()
    {
        await _sut.WriteAsync([Entry("/music/nas/a.mp3", [1])], Token);
        await _sut.ApproveIndividuallyAsync(
            ["/music/nas/a.mp3"], [new FieldAnswer(TrackField.Dance, "Mazurka")], Token);

        await _sut.DeleteMissingAsync([], ["/music/nas/a.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.False(Assert.Single(snapshot).Value.IsAvailable);
        Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
    }

    [Fact]
    public async Task DeleteMissing_AFileFoundAgain_IsReachableAgain()
    {
        await _sut.WriteAsync([Entry("/music/nas/a.mp3", [1])], Token);
        await _sut.DeleteMissingAsync([], ["/music/nas/a.mp3"], Token);

        await _sut.DeleteMissingAsync(["/music/nas/a.mp3"], [], Token);

        Assert.True((await _sut.SnapshotByPathAsync(Token))["/music/nas/a.mp3"].IsAvailable);
    }

    /// <summary>
    /// The watcher's path. A file that comes back on its own is written, and being written is the
    /// whole of the answer: nobody is asked a second time.
    /// </summary>
    [Fact]
    public async Task Write_AfterAPathWasKeptAsUnavailable_MakesItReachableAgain()
    {
        await _sut.WriteAsync([Entry("/music/nas/a.mp3", [1])], Token);
        await _sut.DeleteMissingAsync([], ["/music/nas/a.mp3"], Token);

        await _sut.WriteAsync([Entry("/music/nas/a.mp3", [1])], Token);

        Assert.True((await _sut.SnapshotByPathAsync(Token))["/music/nas/a.mp3"].IsAvailable);
    }

    [Fact]
    public async Task Write_WithNothingToWrite_DoesNotThrow()
    {
        await _sut.WriteAsync([], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

    [Fact]
    public async Task UseBeforeOpen_OpensOnDemand()
    {
        // Not an error: the settings replay and the toolbar badge reach the store before startup's
        // explicit open, and that ordering accident must not become an error toast on every launch.
        using var unopened = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());

        await unopened.WriteAsync([Entry("/music/a.mp3", [9], slug: null)], Token);

        Assert.Single(await unopened.SnapshotByPathAsync(Token));
    }

    [Fact]
    public async Task ACorruptDatabase_IsRebuiltRatherThanFatal()
    {
        // Everything but the approvals is recomputed by the next scan, and a file SQLite cannot
        // open has lost them either way. The old duration cache healed itself the same way.
        _sut.Dispose();
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir.FullName, "library.sqlite"), "this is not a database", Token);

        using var healed = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());
        await healed.WriteAsync([Entry("/music/a.mp3", [9], slug: null)], Token);

        Assert.Single(await healed.SnapshotByPathAsync(Token));
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
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Dance, "scottish")], Token);

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
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Artist, "Naragonia")], Token);

        await _sut.WriteAsync([Entry("/music/renamed.mp3", [7])], Token);

        var approvals = await _sut.ApprovalsAsync(Token);
        Assert.Equal("Naragonia", Assert.Single(approvals[LibraryKey.For([7])]).Value);
    }

    [Fact]
    public async Task ApprovingAField_ReplacesWhateverWasAgreedToBefore()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Title, "First")], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Title, "Second")], Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal("Second", approval.Value);
    }

    [Fact]
    public async Task AnApprovalLandsOnTheAudio_SoBothCopiesOfATrackGetIt()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [9]), Entry("/music/compilation/a.mp3", [9])], Token);

        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Dance, "mazurka")], Token);

        Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([9])]);
    }

    [Fact]
    public async Task RevokingRules_LeavesWhatAPersonAnsweredThemselves()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);
        await _sut.ApproveAsync([ByRule([1], TrackField.Artist, "Naragonia")], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Title, "Le badaud")], Token);

        await _sut.RevokeRuleApprovalsAsync(Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal(TrackField.Title, approval.Field);
    }

    [Fact]
    public async Task ARuleAnsweringAgain_ReplacesAnotherRulesAnswerAndNotAPersonsOwn()
    {
        // Every scan, every retag and every newly declared pattern writes what the rules answered.
        // A rule may take a rule's answer back; a hand correction is the one thing here that cannot
        // be worked out again, and it stays exactly as the person left it.
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Dance, "mazurka")], Token);
        await _sut.ApproveAsync([ByRule([2], TrackField.Dance, "plinn")], Token);

        await _sut.ApproveAsync(
            [ByRule([1], TrackField.Dance, "scottish"), ByRule([2], TrackField.Dance, "scottish")], Token);

        var approvals = await _sut.ApprovalsAsync(Token);
        var byHand = Assert.Single(approvals[LibraryKey.For([1])]);
        Assert.Equal("mazurka", byHand.Value);
        Assert.Equal(ApprovalKind.Individual, byHand.Kind);
        Assert.Null(byHand.Rule);
        Assert.Equal("scottish", Assert.Single(approvals[LibraryKey.For([2])]).Value);
    }

    [Fact]
    public async Task ARuleWritingOverAPersonsAnswer_DoesNotBecomeWhenTheyAgreed()
    {
        // The retag that comes with the rule's write is what has to bring the track back for
        // reconfirmation. Taking the rule's write time as the moment the person agreed is what used
        // to make it pass silently instead.
        var entry = Entry("/music/a.mp3", [1]);
        await _sut.WriteAsync([entry], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Dance, "mazurka")], Token);

        var retagged = entry with { LastWriteUtc = entry.LastWriteUtc.AddHours(3) };
        await _sut.WriteAsync([retagged], Token);
        await _sut.ApproveAsync(
            [ByRule([1], TrackField.Dance, "scottish") with { FileWriteUtc = retagged.LastWriteUtc }], Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal(entry.LastWriteUtc, approval.FileWriteUtc);
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
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Dance, "mazurka")], Token);

        await _sut.DeleteMissingAsync([], [], Token);

        Assert.Empty(await _sut.ApprovalsAsync(Token));
    }

    [Fact]
    public async Task ApprovingViaTheOlderCopy_DoesNotFlagTheNewerOneAsChanged()
    {
        // One audio at two paths with different write times. The approval lands on the audio, so
        // it must carry the newest write time among the copies, or the newer copy reads as
        // "changed since approval" forever and sits in review claiming a retag that never happened.
        var older = Entry("/music/old.mp3", [1], slug: null);
        var newer = Entry("/music/new.mp3", [1], slug: null) with
        {
            LastWriteUtc = older.LastWriteUtc.AddHours(2)
        };
        await _sut.WriteAsync([older, newer], Token);

        await _sut.ApproveIndividuallyAsync(["/music/old.mp3"], [new FieldAnswer(TrackField.Dance, "Mazurka")], Token);

        var approval = Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
        Assert.Equal(newer.LastWriteUtc, approval.FileWriteUtc);
    }

    [Fact]
    public async Task CountIndexed_IsTheNumberOfKnownPaths()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);

        Assert.Equal(2, await _sut.CountIndexedAsync(Token));
    }

    /// <summary>A progress line counting files nothing is going to read never moves.</summary>
    [Fact]
    public async Task CountIndexed_LeavesOutWhatCannotBeReached()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/nas/b.mp3", [2])], Token);

        await _sut.DeleteMissingAsync(["/music/a.mp3"], ["/music/nas/b.mp3"], Token);

        Assert.Equal(1, await _sut.CountIndexedAsync(Token));
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

    [Fact]
    public async Task DeletingAPath_ForgetsTheTrackWhenItWasTheLastOne()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1], slug: null)], Token);
        await _sut.ApproveIndividuallyAsync(["/music/a.mp3"], [new FieldAnswer(TrackField.Dance, "Mazurka")], Token);

        await _sut.DeletePathsAsync(["/music/a.mp3"], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
        Assert.Empty(await _sut.ApprovalsAsync(Token));
    }

    [Fact]
    public async Task DeletingAWholeFolderOfPaths_ForgetsThemAllAndLeavesTheRest()
    {
        // A folder sent to the recycle bin is every path under it in one call, which is the one
        // place the watcher's batch is certain to be large.
        await _sut.WriteAsync(
            [Entry("/music/album/a.mp3", [1]), Entry("/music/album/b.mp3", [2]), Entry("/music/c.mp3", [3])],
            Token);

        await _sut.DeletePathsAsync(["/music/album/a.mp3", "/music/album/b.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Equal(["/music/c.mp3"], snapshot.Keys);
    }

    [Fact]
    public async Task DeletingNoPaths_DoesNotThrow()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.DeletePathsAsync([], Token);

        Assert.Equal(["/music/a.mp3"], (await _sut.SnapshotByPathAsync(Token)).Keys);
    }

    [Fact]
    public async Task ARename_KeepsTheApprovalsWhenTheNewPathIsWrittenFirst()
    {
        // The watcher's rename order: write the new path, then forget the old, so the audio's hash
        // is never unreferenced and the decisions riding on it survive.
        await _sut.WriteAsync([Entry("/music/old.mp3", [1], slug: null)], Token);
        await _sut.ApproveIndividuallyAsync(["/music/old.mp3"], [new FieldAnswer(TrackField.Dance, "Mazurka")], Token);

        await _sut.WriteAsync([Entry("/music/new.mp3", [1], slug: null)], Token);
        await _sut.DeletePathsAsync(["/music/old.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Equal(["/music/new.mp3"], snapshot.Keys);
        Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
    }

    [Fact]
    public async Task MovingAPath_KeepsARowThatCouldNotBeReachedUnreachable()
    {
        // A folder renamed while the drive it lived on is still away. Writing the row at its new
        // path would mark it reachable, and dead paths would walk back into the library; the row
        // moves instead, and being unreachable is as true of it at the new path as at the old.
        await _sut.WriteAsync([Entry("/music/nas/a.mp3", [1])], Token);
        await _sut.DeleteMissingAsync([], ["/music/nas/a.mp3"], Token);

        await _sut.MovePathsAsync([new PathMove("/music/nas/a.mp3", "/music/NAS/a.mp3")], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Equal(["/music/NAS/a.mp3"], snapshot.Keys);
        Assert.False(snapshot["/music/NAS/a.mp3"].IsAvailable);
    }

    [Fact]
    public async Task MovingAPath_KeepsWhatWasDecidedAboutTheTrack()
    {
        // The audio is untouched, so the hash every approval hangs on is untouched, and nothing
        // has to be opened again to work that out.
        await _sut.WriteAsync([Entry("/music/album/a.mp3", [1], slug: null)], Token);
        await _sut.ApproveIndividuallyAsync(
            ["/music/album/a.mp3"], [new FieldAnswer(TrackField.Dance, "Mazurka")], Token);

        await _sut.MovePathsAsync([new PathMove("/music/album/a.mp3", "/music/Naragonia/a.mp3")], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Equal(["/music/Naragonia/a.mp3"], snapshot.Keys);
        Assert.Equal([1], snapshot["/music/Naragonia/a.mp3"].ContentHash);
        Assert.Single((await _sut.ApprovalsAsync(Token))[LibraryKey.For([1])]);
    }

    [Fact]
    public async Task MovingAPathOntoAnother_LeavesOneRowThere()
    {
        // A file dragged over one that was already indexed. One path, one row, which is what a
        // scan would conclude as well.
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);

        await _sut.MovePathsAsync([new PathMove("/music/a.mp3", "/music/b.mp3")], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Equal(["/music/b.mp3"], snapshot.Keys);
        Assert.Equal([1], snapshot["/music/b.mp3"].ContentHash);
    }

    [Fact]
    public async Task MovingNothing_DoesNotThrow()
    {
        await _sut.MovePathsAsync([], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

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
