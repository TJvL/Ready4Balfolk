using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Toolbar;

/// <summary>The toolbar, and the one place a scan is allowed to mention what it could not place.</summary>
/// <remarks>
/// A count on a button, never a dialog and never a toast. New files arrive while the application is
/// running in front of a room, and a tagging question during a bal is the worst possible moment to
/// ask one. The count is a query against the index, so it survives a restart for free.
/// </remarks>
public sealed partial class ToolbarViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>How many tracks are waiting for a person, which is what the gate holds back.</summary>
    [Reactive] public partial int InReviewCount { get; private set; }
    [Reactive] public partial string InReviewText { get; private set; }
    [Reactive] public partial bool HasInReview { get; private set; }

    public ToolbarViewModel(ITrackStore trackStore)
    {
        InReviewText = string.Empty;

        // The gate's own number, pushed whenever the library is rebuilt. A SQL count once decided
        // this independently and missed two of the gate's three reasons to hold a track back.
        trackStore.InReviewCount
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(waiting =>
            {
                InReviewCount = waiting;
                HasInReview = waiting > 0;
                InReviewText = waiting > 0
                    ? string.Format(CultureInfo.CurrentCulture, UiStrings.Toolbar_ReviewCount, waiting)
                    : string.Empty;
            })
            .DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}
