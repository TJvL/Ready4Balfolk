using System;
using System.Globalization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.History;

public readonly struct HistoryItemViewModel(QueueHistoryEntry entry)
{
    public QueueHistoryEntry Entry => entry;

    public string Type => entry switch
    {
        TrackHistoryEntry => UiStrings.History_TypeTrack,
        MessageHistoryEntry => UiStrings.History_TypeMessage,
        DelayHistoryEntry => UiStrings.History_TypeDelay,
        StopHistoryEntry => UiStrings.History_TypeStop,
        _ => ""
    };

    public string Description => entry switch
    {
        TrackHistoryEntry t => $"{t.Dance} \u2014 {t.Artist} \u2014 {t.Title}",
        MessageHistoryEntry m => m.Message,
        DelayHistoryEntry => "Delay",
        StopHistoryEntry => "Stop",
        _ => ""
    };

    public string DurationFormatted => entry switch
    {
        TrackHistoryEntry t => FormatTime(t.Duration),
        MessageHistoryEntry { Duration: { } d } => FormatTime(d),
        DelayHistoryEntry d => FormatTime(d.Duration),
        _ => ""
    };

    // Blank for entries recorded before start times were stored.
    public string StartedAtFormatted =>
        entry.StartedAt is { } startedAt ? startedAt.ToString("HH:mm", CultureInfo.CurrentCulture) : "";

    public string Status => entry.CompletionStatus switch
    {
        CompletionStatus.Finished => UiStrings.History_StatusFinished,
        CompletionStatus.Skipped => UiStrings.History_StatusSkipped,
        _ => ""
    };

    private static string FormatTime(TimeSpan time)
        => $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
}
