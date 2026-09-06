using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Presentation;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// What the room's screen is given to draw. The artist and the title are one line here rather than
/// three controls beside each other on the window, because a row of controls cannot wrap and a
/// projector is the one screen nobody can scroll.
/// </summary>
public sealed class PresentationDisplayViewModelTests : IDisposable
{
    private readonly Subject<PresentationState> _states = new();
    private readonly PresentationDisplayViewModel _sut;

    public PresentationDisplayViewModelTests()
    {
        var presentation = Substitute.For<IPresentationStateService>();
        presentation.WhenStateChanged.Returns(_states);
        presentation.WhenProgressChanged.Returns(Observable.Never<PresentationProgress>());

        _sut = new PresentationDisplayViewModel(presentation);
    }

    private static PresentationItem Track(string dance, string artist, string title) =>
        new(PresentationItemKind.Track, dance, artist, title);

    private void Showing(PresentationItem current) =>
        Showing(current, PresentationItem.None, PresentationItem.None);

    private void Showing(PresentationItem current, PresentationItem next, PresentationItem behind) =>
        _states.OnNext(new PresentationState(current, next, behind, true));

    [Fact]
    public void Track_ArtistAndTitleAreOneLine()
    {
        Showing(Track("Mazurka", "Naragonia", "Salamandre"));

        Assert.Equal("Mazurka", _sut.CurrentDance);
        Assert.Equal("Naragonia - Salamandre", _sut.CurrentTrack);
    }

    [Fact]
    public void TrackWithoutATitle_DoesNotEndInASeparator()
    {
        Showing(Track("Mazurka", "Naragonia", ""));

        Assert.Equal("Naragonia", _sut.CurrentTrack);
    }

    [Fact]
    public void TrackWithoutAnArtist_DoesNotOpenWithASeparator()
    {
        Showing(Track("Mazurka", "", "Salamandre"));

        Assert.Equal("Salamandre", _sut.CurrentTrack);
    }

    [Fact]
    public void TrackWithNeither_SaysNothingRatherThanASeparator()
    {
        Showing(Track("Mazurka", "", ""));

        Assert.Equal("", _sut.CurrentTrack);
    }

    [Fact]
    public void NextAndBehind_AreWrittenTheSameWay()
    {
        Showing(
            Track("Mazurka", "Naragonia", "Salamandre"),
            new PresentationItem(PresentationItemKind.Delay, "", "", ""),
            Track("Scottish", "Trio Loubelya", "La Belle"));

        Assert.Equal(UiStrings.Presentation_Delay, _sut.NextDance);
        Assert.Equal("", _sut.NextTrack);
        Assert.Equal("Scottish", _sut.BehindDance);
        Assert.Equal("Trio Loubelya - La Belle", _sut.BehindTrack);
    }

    [Fact]
    public void MessageComingUp_IsBilledAsAMessageWithItsWordsUnderneath()
    {
        Showing(
            Track("Mazurka", "Naragonia", "Salamandre"),
            new PresentationItem(PresentationItemKind.Message, "Bar closes at eleven", "", ""),
            PresentationItem.None);

        Assert.Equal(UiStrings.Presentation_Message, _sut.NextDance);
        Assert.Equal("Bar closes at eleven", _sut.NextTrack);
    }

    [Fact]
    public void NothingPlaying_LeavesNoTrackOnTheScreen()
    {
        Showing(Track("Mazurka", "Naragonia", "Salamandre"));
        Showing(PresentationItem.None);

        Assert.False(_sut.HasCurrentItem);
        Assert.Equal("", _sut.CurrentTrack);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _states.Dispose();
    }
}
