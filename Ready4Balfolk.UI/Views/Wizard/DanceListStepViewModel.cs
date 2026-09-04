using System;
using System.Globalization;
using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's dance list step: get a list, one way or the other.</summary>
/// <remarks>
/// Nothing to answer. The list is BigBalfolkList's shared vocabulary rather than the user's own, so
/// there is no subset to choose and no spelling to settle here. What there is, is a decision to
/// make: the application ships no list, so this step is where one arrives, by fetching it or by
/// importing a file somebody carried in. It blocks until one has, because a library cannot be
/// answered without a vocabulary and finding that out later is worse than being asked now.
/// </remarks>
public sealed partial class DanceListStepViewModel(
    IDanceListStore store, IDanceListFeed feed, TimeProvider? timeProvider = null)
    : WizardStepViewModel
{
    /// <summary>
    /// How recently the list must have been fetched for this step to leave it alone. The
    /// application refreshes at startup, and on a first run this step is reached seconds later:
    /// without this it would ask BigBalfolkList for the same file twice in one minute.
    /// </summary>
    private static readonly TimeSpan RecentlyFetched = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    [Reactive] public partial string SummaryText { get; private set; }

    /// <summary>Whether the machine has a dance list at all yet.</summary>
    [Reactive] public partial bool HasAList { get; private set; }

    [Reactive] public partial string OriginText { get; private set; }

    [Reactive] public partial bool IsFetching { get; private set; }

    /// <summary>Where the list comes from, opened in the user's own browser.</summary>
    public Uri SourceUri { get; } = feed.HomePage;

    public override string Title => UiStrings.Wizard_DanceList_Title;

    public override string Explanation => UiStrings.Wizard_DanceList_Explanation;

    public override IObservable<bool> CanContinue => this.WhenAnyValue(step => step.HasAList);

    public override IObservable<string> BlockedReason => Observable.Return(UiStrings.Wizard_DanceList_Blocked);

    /// <summary>Shows what the machine already has, and asks for nothing on its own.</summary>
    public override Task EnterAsync()
    {
        Describe(store.Status);
        return Task.CompletedTask;
    }

    /// <summary>Fetches the published list, because the user pressed the button that says so.</summary>
    [ReactiveCommand]
    private async Task FetchAsync()
    {
        // Stepping back and forward over this page is not a reason to ask GitHub again.
        if (WasFetchedRecently(store.Status))
        {
            Describe(store.Status);
            return;
        }

        IsFetching = true;
        try
        {
            await store.RefreshAsync();
        }
        finally
        {
            IsFetching = false;
            Describe(store.Status);
        }
    }

    /// <summary>Takes the list from a file, for the machine that will never reach BigBalfolkList.</summary>
    public async Task ImportAsync(IFileInfo file)
    {
        IsFetching = true;
        try
        {
            await store.UpdateFromFileAsync(file);
        }
        finally
        {
            IsFetching = false;
            Describe(store.Status);
        }
    }

    private bool WasFetchedRecently(DanceListStatus status) =>
        status.Origin is DanceListOrigin.Downloaded or DanceListOrigin.File
        && status.ObtainedAt is { } obtainedAt
        && _timeProvider.GetUtcNow() - obtainedAt < RecentlyFetched;

    private void Describe(DanceListStatus status)
    {
        HasAList = status.Origin is not DanceListOrigin.None && status.DanceCount > 0;

        SummaryText = string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.Wizard_DanceList_Summary,
            status.DanceCount,
            status.TagCount);

        OriginText = status.ObtainedAt is { } obtainedAt
            ? string.Format(
                CultureInfo.CurrentCulture, UiStrings.DanceList_Obtained, obtainedAt.ToLocalTime().DateTime)
            : UiStrings.DanceList_NoListYet;
    }
}
