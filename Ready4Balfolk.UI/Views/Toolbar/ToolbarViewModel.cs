using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Library;
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
    private readonly ILibraryIndex _libraryIndex;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>How many tracks are waiting for a person, which is what the gate holds back.</summary>
    [Reactive] public partial int InReviewCount { get; private set; }
    [Reactive] public partial string InReviewText { get; private set; }
    [Reactive] public partial bool HasInReview { get; private set; }

    public ToolbarViewModel(ILibraryIndex libraryIndex, ITrackStore trackStore, ILoggerService loggerService)
    {
        _libraryIndex = libraryIndex;
        _loggerService = loggerService;
        InReviewText = string.Empty;

        // Refreshed when a load finishes rather than continuously: nothing changes the count while
        // the library is sitting still.
        trackStore.IsLoading
            .DistinctUntilChanged()
            .Where(loading => !loading)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => Refresh())
            .DisposeWith(_disposables);
    }

    public void Refresh() => RefreshAsync().SafeFireAndForget(
        exception => _loggerService.ErrorAsync("Failed to count what is waiting for review", exception));

    public void Dispose() => _disposables.Dispose();

    private async Task RefreshAsync()
    {
        // Unreviewed, not unresolved: a track with a dance nobody has agreed to is still waiting.
        var waiting = await _libraryIndex.CountInReviewAsync();
        InReviewCount = waiting;
        HasInReview = waiting > 0;
        InReviewText = waiting > 0
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.Toolbar_ReviewCount, waiting)
            : string.Empty;
    }
}
