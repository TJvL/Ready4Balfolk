using System.Reactive.Linq;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Views.Discovery;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// The screen's job is that a rule is never agreed to blind, so what it measures and what it stores
/// are the things worth testing. The bindings that put it on screen are not covered by anything.
/// </summary>
public sealed class DiscoveryViewModelTests : IDisposable
{
    private const string Root = "/music";

    private static readonly string[] Files =
    [
        "/music/Bal O'Gadjo/Scottish - Bal O'Gadjo - Le badaud.mp3",
        "/music/Naragonia/Mazurka - Naragonia - Idiosyncrasie.mp3",
        "/music/Naragonia/Valse - Naragonia - La Sauvagine.mp3",
        "/music/TREF/03-Track 3.mp3",
        "/music/10. Hep Harz (Cercle).mp3"
    ];

    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ILibraryIndex _libraryIndex = Substitute.For<ILibraryIndex>();
    private readonly DiscoveryViewModel _sut;

    private ApplicationSettings _stored = new ApplicationSettings() with { MusicDirectoryPath = Root };

    public DiscoveryViewModelTests()
    {
        _settingsStore.Current.Returns(_ => _stored);
        _settingsStore.UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>())
            .Returns(call =>
            {
                _stored = call.Arg<Func<ApplicationSettings, ApplicationSettings>>()!(_stored);
                return Task.CompletedTask;
            });

        _libraryIndex.OpenAsync().Returns(Task.CompletedTask);
        _libraryIndex.SnapshotByPathAsync().Returns(Files.ToDictionary(
            path => path,
            path => new LibraryEntry
            {
                ContentHash = [1],
                Path = path,
                FileSize = 1,
                LastWriteUtc = DateTime.UnixEpoch,
                Duration = TimeSpan.FromMinutes(3),
                Format = AudioFormat.Mp3
            }));

        var trackStore = Substitute.For<ITrackStore>();
        trackStore.IsLoading.Returns(Observable.Return(false));

        var danceListStore = Substitute.For<IDanceListStore>();
        danceListStore.Index.Returns(DanceListIndex.Empty);

        _sut = new DiscoveryViewModel(
            _settingsStore, _libraryIndex, danceListStore, trackStore, Substitute.For<ILoggerService>());
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task ADraft_IsMeasuredBeforeItIsAgreedTo()
    {
        await Refresh();

        _sut.DraftPattern = "%d - %a - %t";
        Preview();

        Assert.Contains("3", _sut.DraftSummary);
        Assert.True(_sut.CanDeclareDraft);
        Assert.NotEmpty(_sut.DraftSamples);
        Assert.NotEmpty(_sut.DraftMisses);
    }

    [Fact]
    public async Task ADraftShows_WhatItWouldMakeOfTheFiles()
    {
        await Refresh();

        _sut.DraftPattern = "%d - %a - %t";
        Preview();

        var sample = _sut.DraftSamples[0];
        Assert.Equal("Scottish", sample.Dance);
        Assert.Equal("Bal O'Gadjo", sample.Artist);
        Assert.Equal("Le badaud", sample.Title);
    }

    [Fact]
    public async Task ABadDraft_CannotBeDeclared()
    {
        await Refresh();

        _sut.DraftPattern = "%a%t";
        Preview();

        Assert.False(_sut.CanDeclareDraft);
    }

    [Fact]
    public async Task ARuleAlreadyDeclared_IsNotOfferedTwice()
    {
        await Refresh();
        await Declare("%d - %a - %t");

        _sut.DraftPattern = "%d - %a - %t";
        Preview();

        Assert.False(_sut.CanDeclareDraft);
    }

    [Fact]
    public async Task DeclaringARule_StoresItAndShowsWhatIsLeft()
    {
        await Refresh();

        await Declare("%d - %a - %t");

        Assert.Equal(["%d - %a - %t"], _stored.Discovery.FileNamePatterns);
        Assert.Single(_sut.Patterns);
        Assert.Contains("3", _sut.CoverageSummary);
        Assert.Equal(string.Empty, _sut.DraftPattern);
    }

    [Fact]
    public async Task TheNextDraft_IsMeasuredAgainstWhatIsLeft()
    {
        // Declare one, it swallows most of the library, and the honest question about the next rule
        // is what it does to the pile that is actually left.
        await Refresh();
        await Declare("%d - %a - %t");

        _sut.DraftPattern = "%n. %t";
        Preview();

        Assert.Contains("1", _sut.DraftSummary);
        Assert.Equal(2, _sut.DraftMisses.Count + _sut.DraftSamples.Count);
    }

    [Fact]
    public async Task RemovingARule_TakesBackWhatItAnswered()
    {
        await Refresh();
        await Declare("%d - %a - %t");

        await _sut.RemovePatternCommand.Execute(_sut.Patterns[0]);

        Assert.Empty(_stored.Discovery.FileNamePatterns);
        Assert.Empty(_sut.Patterns);
    }

    [Fact]
    public async Task RulesCanBeReordered_BecauseOrderIsTheAnswer()
    {
        await Refresh();
        await Declare("%d - %a - %t");
        await Declare("%a - %t");

        await _sut.MovePatternUpCommand.Execute(_sut.Patterns[1]);

        Assert.Equal(["%a - %t", "%d - %a - %t"], _stored.Discovery.FileNamePatterns);
    }

    [Fact]
    public async Task AFolderLevel_IsOfferedOnlyWhereTheLibraryHasOne()
    {
        await Refresh();

        // One file sits in the root and the rest one deep, so there is exactly one level to give a
        // role to.
        Assert.Single(_sut.Levels);
        Assert.Equal(1, _sut.Levels[0].Level);
    }

    [Fact]
    public async Task AFolderRole_IsStoredWhenItIsApplied()
    {
        await Refresh();

        _sut.Levels[0].Role = FolderRole.Artist;
        await _sut.ApplyRolesAndTagsCommand.Execute();

        Assert.Equal([FolderRole.Artist], _stored.Discovery.FolderRoles);
    }

    [Fact]
    public async Task LeavingTagsAlone_StoresNoDeclarationAtAll()
    {
        // Null is not an empty list here: it is "the application's guess still applies", and storing
        // the guess as a declaration would silently promote it to the top tier.
        await Refresh();

        await _sut.ApplyRolesAndTagsCommand.Execute();

        Assert.Null(_stored.Discovery.TagTrust.Artist);
        Assert.Null(_stored.Discovery.TagTrust.Dance);
    }

    [Fact]
    public async Task DeclaringATagField_StoresTheList()
    {
        await Refresh();

        var dance = _sut.TagFields[0];
        dance.UsesDefault = false;
        dance.Toggles.First(toggle => toggle.Field == TagField.Comment).IsTrusted = true;

        await _sut.ApplyRolesAndTagsCommand.Execute();

        Assert.Equal([TagField.Comment], _stored.Discovery.TagTrust.Dance);
    }

    [Fact]
    public async Task TheLibraryIsMeasuredAndOffered()
    {
        // Five files is under the threshold for a rule, so nothing is proposed from them: three
        // files that happen to look alike is a coincidence, not a rule worth a greenlight.
        await Refresh();

        Assert.Empty(_sut.Proposals);
    }

    [Fact]
    public async Task AcceptingAProposal_DeclaresIt()
    {
        await Refresh();

        var proposal = ProposalViewModel.From(new FolderRoleProposal
        {
            Level = 1,
            Role = FolderRole.Artist,
            Agreeing = 96,
            Considered = 121,
            Samples = ["Naragonia"]
        });

        await _sut.AcceptProposalCommand.Execute(proposal);

        Assert.Equal([FolderRole.Artist], _stored.Discovery.FolderRoles);
    }

    [Fact]
    public async Task AShapeNothingCouldBeNamedIn_CannotBeDeclared()
    {
        await Refresh();

        var proposal = ProposalViewModel.From(new ShapeProposal
        {
            Separator = " - ",
            Fields = 2,
            Files = 40,
            Considered = 100,
            Positions = [new PositionFinding(1, null, 0, 0, 40, 0, 0, 40)],
            Samples = ["07 - 08"],
            Pattern = null
        });

        Assert.False(proposal.CanAccept);

        await _sut.AcceptProposalCommand.Execute(proposal);

        Assert.Empty(_stored.Discovery.FileNamePatterns);
    }

    [Fact]
    public async Task TurningAProposalDown_LeavesTheSettingsAlone()
    {
        await Refresh();
        var proposal = ProposalViewModel.From(new FolderRoleProposal
        {
            Level = 1,
            Role = FolderRole.Artist,
            Agreeing = 96,
            Considered = 121,
            Samples = []
        });
        _sut.Proposals.Add(proposal);

        await _sut.DismissProposalCommand.Execute(proposal);

        Assert.Empty(_sut.Proposals);
        Assert.Empty(_stored.Discovery.FolderRoles);
    }

    private async Task Refresh() => await _sut.RefreshCommand.Execute();

    private async Task Declare(string pattern)
    {
        _sut.DraftPattern = pattern;
        Preview();
        await _sut.DeclareDraftCommand.Execute();
    }

    /// <summary>
    /// The screen previews as the pattern is typed, on a throttle. A test should not wait out a
    /// timer, so it asks for the same measurement directly.
    /// </summary>
    private void Preview() => _sut.PreviewDraftNow();
}
