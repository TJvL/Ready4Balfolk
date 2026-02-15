using System;
using Ready4Balfolk.Domain.Models.History;

namespace Ready4Balfolk.UI.Views.History;

public readonly struct HistoryItemViewModel(QueueHistoryEntry entry)
{
    public QueueHistoryEntry Entry => entry;

    public string Type => entry switch
    {
        TrackHistoryEntry => "Track",
        MessageHistoryEntry => "Message",
        DelayHistoryEntry => "Delay",
        StopHistoryEntry => "Stop",
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

    public string Status => entry.CompletionStatus switch
    {
        CompletionStatus.Finished => "Finished",
        CompletionStatus.Skipped => "Skipped",
        _ => ""
    };

    private static string FormatTime(TimeSpan time)
        => $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
}
