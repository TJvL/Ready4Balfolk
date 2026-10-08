// Written by hand, not generated: one property per entry in DomainStrings.resx, named after its key.
// No tool produces this shape (a static class, nullable annotations, a non-null string per key), so
// nothing may regenerate it, and the project file no longer names a generator that would.
// scripts/check-translations.py fails when a property and a key in DomainStrings.resx stop matching.
// The analyzers still pass over it, by its .Designer.cs name rather than by any header, which is
// what lets each property keep its key's underscore.

#nullable enable

namespace Ready4Balfolk.Domain.Resources;

using System.Globalization;
using System.Resources;

public static class DomainStrings
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "Ready4Balfolk.Domain.Resources.DomainStrings",
            typeof(DomainStrings).Assembly);

    public static CultureInfo? Culture { get; set; }

    // Queue guard rules
    public static string AutoTrackRule_OnlyOneAllowed =>
        ResourceManager.GetString("AutoTrackRule_OnlyOneAllowed", Culture)!;

    public static string QueueCutoffRule_PastCutoff =>
        ResourceManager.GetString("QueueCutoffRule_PastCutoff", Culture)!;

    public static string DuplicateTrackRule_AlreadyInQueue =>
        ResourceManager.GetString("DuplicateTrackRule_AlreadyInQueue", Culture)!;

    public static string DuplicateTrackRule_CurrentlyPlaying =>
        ResourceManager.GetString("DuplicateTrackRule_CurrentlyPlaying", Culture)!;

    public static string DuplicateTrackRule_AlreadyPlayed =>
        ResourceManager.GetString("DuplicateTrackRule_AlreadyPlayed", Culture)!;

    public static string MaxItemsRule_QueueFull =>
        ResourceManager.GetString("MaxItemsRule_QueueFull", Culture)!;

    public static string QueueGuard_DeniedByRule =>
        ResourceManager.GetString("QueueGuard_DeniedByRule", Culture)!;

    // Editor action descriptions
    // DanceTreeAction validation errors
    // DanceSynonymAction descriptions
    // DanceSynonymAction validation errors
    // Default names
    // Queue item descriptions
    public static string StopQueueItem_Description =>
        ResourceManager.GetString("StopQueueItem_Description", Culture)!;

    public static string DelayQueueItem_Description =>
        ResourceManager.GetString("DelayQueueItem_Description", Culture)!;

    public static string GapQueueItem_Description =>
        ResourceManager.GetString("GapQueueItem_Description", Culture)!;

    public static string EndOfNightQueueItem_Description =>
        ResourceManager.GetString("EndOfNightQueueItem_Description", Culture)!;

    // Queue rules
    public static string EndOfNightRule_EveningEnded =>
        ResourceManager.GetString("EndOfNightRule_EveningEnded", Culture)!;

    // Queue and dance list
    public static string Queue_CannotPlay =>
        ResourceManager.GetString("Queue_CannotPlay", Culture)!;

    public static string Queue_AdvanceFailed =>
        ResourceManager.GetString("Queue_AdvanceFailed", Culture)!;

    public static string Queue_PreloadFailed =>
        ResourceManager.GetString("Queue_PreloadFailed", Culture)!;

    public static string DanceList_InvalidJson =>
        ResourceManager.GetString("DanceList_InvalidJson", Culture)!;

    public static string DanceList_NoDances =>
        ResourceManager.GetString("DanceList_NoDances", Culture)!;

    public static string DanceList_WrongFormatVersion =>
        ResourceManager.GetString("DanceList_WrongFormatVersion", Culture)!;

    public static string DanceList_DuplicateNames =>
        ResourceManager.GetString("DanceList_DuplicateNames", Culture)!;

    public static string DanceList_Invalid =>
        ResourceManager.GetString("DanceList_Invalid", Culture)!;

    public static string Audio_OutputGone =>
        ResourceManager.GetString("Audio_OutputGone", Culture)!;

    // The night written out for a rights organisation
    public static string NightReport_Heading =>
        ResourceManager.GetString("NightReport_Heading", Culture)!;

    public static string NightReport_TimeColumn =>
        ResourceManager.GetString("NightReport_TimeColumn", Culture)!;

    public static string NightReport_ArtistColumn =>
        ResourceManager.GetString("NightReport_ArtistColumn", Culture)!;

    public static string NightReport_TitleColumn =>
        ResourceManager.GetString("NightReport_TitleColumn", Culture)!;


    // What the DJ is told when something the domain does fails. The log says it in English, separately.
    public static string Settings_Unreadable =>
        ResourceManager.GetString("Settings_Unreadable", Culture)!;

    public static string Settings_UnreadableKept =>
        ResourceManager.GetString("Settings_UnreadableKept", Culture)!;

    public static string Settings_CouldNotOpen =>
        ResourceManager.GetString("Settings_CouldNotOpen", Culture)!;

    public static string Settings_SaveFailed =>
        ResourceManager.GetString("Settings_SaveFailed", Culture)!;

    public static string DanceList_Unreachable =>
        ResourceManager.GetString("DanceList_Unreachable", Culture)!;

    public static string DanceList_FileUnreadable =>
        ResourceManager.GetString("DanceList_FileUnreadable", Culture)!;

    public static string DanceList_CacheFailed =>
        ResourceManager.GetString("DanceList_CacheFailed", Culture)!;

    public static string History_ReadFailed =>
        ResourceManager.GetString("History_ReadFailed", Culture)!;

    public static string History_ListNightsFailed =>
        ResourceManager.GetString("History_ListNightsFailed", Culture)!;

    public static string History_ReadNightFailed =>
        ResourceManager.GetString("History_ReadNightFailed", Culture)!;

    public static string History_WriteFailed =>
        ResourceManager.GetString("History_WriteFailed", Culture)!;

    public static string History_EndNightFailed =>
        ResourceManager.GetString("History_EndNightFailed", Culture)!;

    public static string History_DeleteNightFailed =>
        ResourceManager.GetString("History_DeleteNightFailed", Culture)!;

    public static string Library_RebuildFailed =>
        ResourceManager.GetString("Library_RebuildFailed", Culture)!;

    public static string Library_ScanWriteFailed =>
        ResourceManager.GetString("Library_ScanWriteFailed", Culture)!;

    public static string Library_ChangesFailed =>
        ResourceManager.GetString("Library_ChangesFailed", Culture)!;

    public static string Library_IndexRebuilt =>
        ResourceManager.GetString("Library_IndexRebuilt", Culture)!;

    public static string Library_AnswersLost =>
        ResourceManager.GetString("Library_AnswersLost", Culture)!;

    public static string Audio_NeverCameUp =>
        ResourceManager.GetString("Audio_NeverCameUp", Culture)!;
}
