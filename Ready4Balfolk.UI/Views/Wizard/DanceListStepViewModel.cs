using System;
using System.Globalization;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's dance list step: fetch the published list, and show what arrived.</summary>
/// <remarks>
/// Nothing to answer. The list is BigBalfolkList's shared vocabulary rather than the user's own
/// list, so there is no subset to choose and no spelling to settle here: a dance nobody owns a
/// track for can never come up anyway. Never blocks either, because the copy shipped with the
/// application is a perfectly good list and a hall with no wifi is an ordinary place to start in.
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

    [Reactive] public partial string OriginText { get; private set; }

    [Reactive] public partial bool IsFetching { get; private set; }

    /// <summary>Where the list comes from, opened in the user's own browser.</summary>
    public Uri SourceUri { get; } = feed.HomePage;

    public override string Title => UiStrings.Wizard_DanceList_Title;

    public override string Explanation => UiStrings.Wizard_DanceList_Explanation;

    public override async Task EnterAsync()
    {
        // Stepping back and forward over this page is not a reason to fetch again either.
        if (WasFetchedRecently(store.Status))
        {
            Describe(store.Status);
            return;
        }

        IsFetching = true;
        try
        {
            // Whatever comes back, there is a list: the fetch either replaces the built-in copy or
            // leaves it standing.
            await store.RefreshAsync();
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
        SummaryText = string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.Wizard_DanceList_Summary,
            status.DanceCount,
            status.TagCount);

        OriginText = status.ObtainedAt is { } obtainedAt
            ? string.Format(
                CultureInfo.CurrentCulture, UiStrings.DanceList_Obtained, obtainedAt.ToLocalTime().DateTime)
            : UiStrings.DanceList_ObtainedBuiltIn;
    }
}
