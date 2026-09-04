using System;
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
    private readonly CompositeDisposable _disposables = [];

    // Current item properties
    [Reactive] public partial string CurrentDance { get; set; }
    [Reactive] public partial string CurrentArtist { get; set; }
    [Reactive] public partial string CurrentTitle { get; set; }
    [Reactive] public partial bool HasCurrentItem { get; set; }
    [Reactive] public partial bool IsMessageMode { get; set; }
    [Reactive] public partial TimeSpan Duration { get; set; }
    [Reactive] public partial TimeSpan Progress { get; set; }
    [Reactive] public partial string CurrentTimeLeft { get; set; }

    // Next item properties
    [Reactive] public partial string NextDance { get; set; }
    [Reactive] public partial string NextArtist { get; set; }
    [Reactive] public partial string NextTitle { get; set; }
    [Reactive] public partial bool HasNextItem { get; set; }

    // The dance waiting behind a pause, shown under it rather than instead of it.
    [Reactive] public partial string BehindDance { get; set; }
    [Reactive] public partial string BehindArtist { get; set; }
    [Reactive] public partial string BehindTitle { get; set; }
    [Reactive] public partial bool HasBehindItem { get; set; }

    public PresentationDisplayViewModel(IPresentationStateService presentationState)
    {
        ArgumentNullException.ThrowIfNull(presentationState);

        CurrentDance = "";
        CurrentArtist = "";
        CurrentTitle = "";
        NextDance = "";
        NextArtist = "";
        NextTitle = "";
        BehindDance = "";
        BehindArtist = "";
        BehindTitle = "";
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
        CurrentArtist = state.Current.Artist;
        CurrentTitle = state.Current.Title;

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
        BehindArtist = state.Behind.Artist;
        BehindTitle = state.Behind.Title;

        // A queued announcement is billed as "Message" with its text beneath, rather than shouting
        // the whole announcement in the next-up slot before its turn.
        if (state.Next.Kind is PresentationItemKind.Message)
        {
            NextDance = UiStrings.Presentation_Message;
            NextArtist = state.Next.Primary;
            NextTitle = "";
            return;
        }

        NextDance = Label(state.Next);
        NextArtist = state.Next.Artist;
        NextTitle = state.Next.Title;
    }

    /// <summary>
    /// The large line. A track and a message carry their own text; a delay, a stop and the end of
    /// the night deliberately do not, so that the window and the browser can each say it in their
    /// own words.
    /// </summary>
    private static string Label(PresentationItem item) => item.Kind switch
    {
        PresentationItemKind.Delay => UiStrings.Presentation_Delay,
        PresentationItemKind.Stop => UiStrings.Presentation_Stop,
        PresentationItemKind.EndOfNight => UiStrings.Presentation_EndOfNight,
        _ => item.Primary
    };

    private static string FormatTimeLeft(TimeSpan remaining) =>
        $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";

    public void Dispose() => _disposables.Dispose();
}
