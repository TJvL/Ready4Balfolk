using System.Reactive.Subjects;
using NSubstitute;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Views.Toolbar;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// The review count on the toolbar button.
/// </summary>
/// <remarks>
/// A count on a button is the only way a scan is allowed to mention what it could not place. New
/// files arrive while the application is running in front of a room, and a tagging question during
/// a bal is the worst possible moment to ask one.
/// </remarks>
public sealed class ToolbarViewModelTests : IDisposable
{
    private readonly BehaviorSubject<int> _inReview = new(0);
    private readonly ToolbarViewModel _sut;

    public ToolbarViewModelTests()
    {
        var trackStore = Substitute.For<ITrackStore>();
        trackStore.InReviewCount.Returns(_inReview);
        _sut = new ToolbarViewModel(trackStore);
    }

    [Fact]
    public void NothingWaiting_ShowsNoBadge()
    {
        Assert.Equal(0, _sut.InReviewCount);
        Assert.False(_sut.HasInReview);
        Assert.Equal(string.Empty, _sut.InReviewText);
    }

    [Fact]
    public void SomethingWaiting_ShowsTheCount()
    {
        _inReview.OnNext(7);

        Assert.Equal(7, _sut.InReviewCount);
        Assert.True(_sut.HasInReview);
        Assert.Contains("7", _sut.InReviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void BackToNothing_ClearsTheBadgeRatherThanShowingZero()
    {
        // Answering the last track in review has to leave the toolbar quiet, not showing "0".
        _inReview.OnNext(3);
        _inReview.OnNext(0);

        Assert.False(_sut.HasInReview);
        Assert.Equal(string.Empty, _sut.InReviewText);
    }

    [Fact]
    public void TheSameCountTwice_IsNotRepublished()
    {
        // The library is rebuilt on every approval, and most rebuilds do not change this number.
        var seen = 0;
        using var subscription = _sut.WhenAnyValue(vm => vm.InReviewCount).Subscribe(_ => seen++);

        _inReview.OnNext(4);
        _inReview.OnNext(4);
        _inReview.OnNext(4);

        // The replay of the initial value, then one change.
        Assert.Equal(2, seen);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _inReview.Dispose();
    }
}
