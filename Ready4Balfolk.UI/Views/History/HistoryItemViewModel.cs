using System;
using System.Globalization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.History;

/// <summary>One line of the night: something that happened, or where the night begins and ends.</summary>
/// <remarks>
/// The boundaries are rows rather than a heading somewhere else, because they belong in the account
/// in the order they happened. Without them a list of entries says nothing about which evening it
/// is or when it started, which is the question somebody scrolling back has.
/// </remarks>
public sealed class HistoryItemViewModel
{
    private readonly QueueHistoryEntry? _entry;

    private HistoryItemViewModel(QueueHistoryEntry? entry, string markerText, string trackTemplate)
    {
        _entry = entry;
        MarkerText = markerText;
        TrackTemplate = trackTemplate;
    }

    /// <summary>How this row writes a track, handed in by the screen that built it.</summary>
    private string TrackTemplate { get; }

    public static HistoryItemViewModel ForEntry(QueueHistoryEntry entry, string trackTemplate) =>
        new(entry, string.Empty, trackTemplate);

    /// <summary>The line that says the night began, which is the first thing in it.</summary>
    public static HistoryItemViewModel ForNightStart(DateTime startedAt) =>
        new(null, string.Format(CultureInfo.CurrentCulture, UiStrings.History_NightStarted, When(startedAt)), "");

    /// <summary>The line that says the night was called, on a night that has been.</summary>
    public static HistoryItemViewModel ForNightEnd(DateTime endedAt) =>
        new(null, string.Format(CultureInfo.CurrentCulture, UiStrings.History_NightEnded, When(endedAt)), "");

    /// <summary>Whether this row is a boundary of the night rather than something that happened.</summary>
    public bool IsMarker => _entry is null;

    public string MarkerText { get; }

    public string Type => _entry switch
    {
        TrackHistoryEntry => UiStrings.History_TypeTrack,
        MessageHistoryEntry => UiStrings.History_TypeMessage,
        DelayHistoryEntry => UiStrings.History_TypeDelay,
        StopHistoryEntry => UiStrings.History_TypeStop,
        EndOfNightHistoryEntry => UiStrings.History_TypeEndOfNight,
        _ => ""
    };

    public string Description => _entry switch
    {
        // Written the way the user asked for it. What was recorded is the fields, so a template
        // changed tonight also changes how last month reads, which is the point of it.
        TrackHistoryEntry t => TrackTextTemplate.Render(TrackTemplate, t.Dance, t.Artist, t.Title),
        MessageHistoryEntry m => m.Message,
        DelayHistoryEntry => UiStrings.History_TypeDelay,
        StopHistoryEntry => UiStrings.History_TypeStop,
        EndOfNightHistoryEntry => UiStrings.History_TypeEndOfNight,
        _ => ""
    };

    /// <summary>
    /// How long it actually ran, which is not how long it is.
    /// </summary>
    /// <remarks>
    /// Measured from the start to the finish where both were recorded: a track that was skipped
    /// after forty seconds ran for forty seconds, whatever the file's length says. Entries written
    /// before finishes were recorded fall back to the length, which is all they know.
    /// </remarks>
    public string DurationFormatted => _entry switch
    {
        null => "",
        { StartedAt: { } started, FinishedAt: { } finished } => FormatTime(finished - started),
        TrackHistoryEntry t => FormatTime(t.Duration),
        MessageHistoryEntry { Duration: { } d } => FormatTime(d),
        DelayHistoryEntry d => FormatTime(d.Duration),
        EndOfNightHistoryEntry { Duration: { } e } => FormatTime(e),
        _ => ""
    };

    // Blank for entries recorded before these times were stored.
    public string StartedAtFormatted => _entry?.StartedAt is { } startedAt ? When(startedAt) : "";

    public string FinishedAtFormatted => _entry?.FinishedAt is { } finishedAt ? When(finishedAt) : "";

    public string Status => _entry?.CompletionStatus switch
    {
        CompletionStatus.Finished => UiStrings.History_StatusFinished,
        CompletionStatus.Skipped => UiStrings.History_StatusSkipped,
        CompletionStatus.FileMissing => UiStrings.History_StatusFileMissing,
        _ => ""
    };

    private static string When(DateTime value) => value.ToString("HH:mm", CultureInfo.CurrentCulture);

    private static string FormatTime(TimeSpan time)
        => $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
}
