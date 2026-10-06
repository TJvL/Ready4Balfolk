using System.Globalization;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>What the DJ reads about the dance list: where it came from, and what came of asking.</summary>
/// <remarks>
/// <para>
/// The setup step and the dance panel fetch and import the same list, and used to say so each in
/// its own way: the step threw away what the store answered, so a fetch that failed in the setup
/// ended with the spinner gone and nothing said, and an import it could not take was told only in
/// the store's own English. Both read it from here now, so they cannot drift apart again.
/// </para>
/// <para>
/// A failed update is a warning, not an error: whatever list was in hand carries on working, and
/// a hall with no wifi is the ordinary case rather than the application breaking. When there is no
/// list in hand, which is where the setup step is, it is not said that one is still in use.
/// </para>
/// </remarks>
internal static class DanceListReports
{
    /// <summary>Tells the DJ what came of an update, once.</summary>
    /// <remarks>
    /// <paramref name="status" /> is the list in hand after the update, which decides how a
    /// failure is put.
    /// </remarks>
    public static void Show(this INotificationService notifications, DanceListUpdate update, DanceListStatus status)
    {
        switch (update.Outcome)
        {
            case DanceListUpdateOutcome.Updated:
                notifications.Show(
                    string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_Updated, update.DancesAdded),
                    NotificationSeverity.Information);
                break;

            case DanceListUpdateOutcome.AlreadyCurrent:
                notifications.Show(UiStrings.DanceList_AlreadyCurrent, NotificationSeverity.Information);
                break;

            case DanceListUpdateOutcome.Failed:
            default:
                notifications.Show(Failed(update.Problem, status), NotificationSeverity.Warning);
                break;
        }
    }

    /// <summary>Why no list was taken, and whether the one in hand still is.</summary>
    public static string Failed(string? problem, DanceListStatus status) => string.Format(
        CultureInfo.CurrentCulture,
        HasAList(status) ? UiStrings.DanceList_UpdateFailed : UiStrings.DanceList_NoneArrived,
        problem);

    /// <summary>Where the list in hand came from and when, so a stale one is visible rather than assumed.</summary>
    public static string Origin(DanceListStatus status) =>
        status.ObtainedAt is { } obtainedAt
            ? string.Format(
                CultureInfo.CurrentCulture, UiStrings.DanceList_Obtained, obtainedAt.ToLocalTime().DateTime)
            : UiStrings.DanceList_NoListYet;

    /// <summary>Whether the machine has a dance list at all yet.</summary>
    public static bool HasAList(DanceListStatus status) =>
        status.Origin is not DanceListOrigin.None && status.DanceCount > 0;
}
