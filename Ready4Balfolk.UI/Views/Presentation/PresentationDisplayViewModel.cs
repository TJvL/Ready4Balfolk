using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Presentation;

/// <summary>Binds the shared presentation state to the desktop display window.</summary>
/// <remarks>
/// The mapping from queue items to what a screen shows lives in
/// <see cref="IPresentationStateService"/>, so this and the browser draw the same pictures from the
/// same source. All that is left here is localizing the kinds that carry no text of their own,
/// and shaping the rest for XAML bindings.
/// </remarks>
public sealed partial class PresentationDisplayViewModel : ReactiveObject, IDisposable
{
    /// <summary>
    /// Fixed, unlike the four templates in the settings: those shape the panels the DJ reads, and
    /// this is the room's screen.
    /// </summary>
    private const string TrackLineTemplate = "%a - %t";

    private readonly CompositeDisposable _disposables = [];

    // Current item properties
    [Reactive] public partial string CurrentDance { get; set; }
    [Reactive] public partial string CurrentTrack { get; set; }
    [Reactive] public partial bool HasCurrentItem { get; set; }
    [Reactive] public partial bool IsMessageMode { get; set; }
    [Reactive] public partial TimeSpan Duration { get; set; }
    [Reactive] public partial TimeSpan Progress { get; set; }
    [Reactive] public partial string CurrentTimeLeft { get; set; }

    // Next item properties
    [Reactive] public partial string NextDance { get; set; }
    [Reactive] public partial string NextTrack { get; set; }
    [Reactive] public partial bool HasNextItem { get; set; }

    // The dance waiting behind a pause, shown under it rather than instead of it.
    [Reactive] public partial string BehindDance { get; set; }
    [Reactive] public partial string BehindTrack { get; set; }
    [Reactive] public partial bool HasBehindItem { get; set; }

    public PresentationDisplayViewModel(IPresentationStateService presentationState)
    {
        ArgumentNullException.ThrowIfNull(presentationState);

        CurrentDance = "";
        CurrentTrack = "";
        NextDance = "";
        NextTrack = "";
        BehindDance = "";
        BehindTrack = "";
        CurrentTimeLeft = "";

        presentationState.WhenStateChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(Apply)
            .DisposeWith(_disposables);

        presentationState.WhenProgressChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(progress =>
            {
                Progress = progress.Elapsed;
                Duration = progress.Duration;
                CurrentTimeLeft = FormatTimeLeft(progress.Remaining);
            })
            .DisposeWith(_disposables);
    }

    private void Apply(PresentationState state)
    {
        HasCurrentItem = state.HasCurrent;
        IsMessageMode = state.Current.Kind is PresentationItemKind.Message;
        CurrentDance = Label(state.Current);
        CurrentTrack = TrackLine(state.Current);

        if (!state.HasCurrent)
        {
            Progress = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            CurrentTimeLeft = "";
        }

        HasNextItem = state.HasNext;

        // The dance the pause is for. A delay or a stop is often queued so the room can make lines
        // or find a partner, and it is exactly then that the floor wants to know what for.
        HasBehindItem = state.HasBehind;
        BehindDance = state.Behind.Primary;
        BehindTrack = TrackLine(state.Behind);

        // A queued announcement is billed as "Message" with its text beneath, rather than shouting
        // the whole announcement in the next-up slot before its turn. A timed one says for how long,
        // since that is the one thing the room cannot read off a screen showing only the words.
        if (state.Next.Kind is PresentationItemKind.Message)
        {
            NextDance = state.Next.Duration is { } messageDuration
                ? string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_MessageWithDuration,
                    DurationPhrase(messageDuration))
                : UiStrings.Presentation_Message;
            NextTrack = state.Next.Primary;
            return;
        }

        NextDance = NextLabel(state.Next);
        NextTrack = TrackLine(state.Next);
    }

    /// <summary>
    /// The large line. A track and a message carry their own text; a delay, a stop and the end of
    /// the night deliberately do not, so that the window and the browser can each say it in their
    /// own words.
    /// </summary>
    private static string Label(PresentationItem item) => item.Kind switch
    {
        PresentationItemKind.Delay => UiStrings.Presentation_Delay,
        PresentationItemKind.Gap => UiStrings.Presentation_Gap,
        PresentationItemKind.Stop => UiStrings.Presentation_Stop,
        PresentationItemKind.EndOfNight => UiStrings.Presentation_EndOfNight,
        _ => item.Primary
    };

    /// <summary>
    /// The label for what is queued up next. A delay says how long it lasts, because there is no
    /// countdown bar under it yet to say so instead, the way there is once it starts playing.
    /// </summary>
    private static string NextLabel(PresentationItem item) =>
        item.Kind is PresentationItemKind.Delay && item.Duration is { } delayDuration
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_DelayWithDuration,
                DurationPhrase(delayDuration))
            : Label(item);

    /// <summary>How long, in words: seconds for a short wait, minutes once it stops being one.</summary>
    private static string DurationPhrase(TimeSpan duration)
    {
        var totalSeconds = (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero);
        if (totalSeconds < 60)
        {
            return totalSeconds == 1
                ? UiStrings.Presentation_OneSecond
                : string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_Seconds, totalSeconds);
        }

        var minutes = Math.Max(1, (int)Math.Round(totalSeconds / 60d, MidpointRounding.AwayFromZero));
        return minutes == 1
            ? UiStrings.Presentation_OneMinute
            : string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_Minutes, minutes);
    }

    /// <summary>The small line: who plays it and what it is called, as one line of text.</summary>
    /// <remarks>
    /// One line rather than three controls side by side. A row of artist, separator and title
    /// cannot wrap, so at the size a hall reads from it runs off both edges of the screen, and the
    /// separator has to be hidden by hand for every track that has no title. The template does both:
    /// it writes the whole line, and a field with nothing in it takes its separator with it.
    /// </remarks>
    private static string TrackLine(PresentationItem item) =>
        TrackTextTemplate.Render(TrackLineTemplate, string.Empty, item.Artist, item.Title);

    private static string FormatTimeLeft(TimeSpan remaining) =>
        $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";

    public void Dispose() => _disposables.Dispose();
}
