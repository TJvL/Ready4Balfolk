using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>One track waiting for a person, with every field shown next to where it came from.</summary>
/// <remarks>
/// The row is the unit of work, not the distinct value: a file that says nothing at all has to be
/// answerable, and a value-shaped list can only hold the ones that said something wrong.
/// </remarks>
public sealed partial class ReviewRowViewModel : ReactiveObject
{
    private readonly IReadOnlyList<string> _allDances;

    // True while a pick is being written into Dance, so the picker does not reopen on its own text.
    private bool _taking;

    private IDisposable? _flashReset;

    public ReviewRowViewModel(ReviewTrack track, bool isFirstOfGroup, IReadOnlyList<string> allDances)
    {
        _allDances = allDances;
        DanceMatches = [];
        Track = track;
        IsFirstOfGroup = isFirstOfGroup;
        FileName = track.FileName;
        FolderText = track.Folder.Length == 0 ? UiStrings.Review_LooseFiles : track.Folder;

        Dance = track.Review.Dance.Value ?? string.Empty;
        Artist = track.Review.Artist.Value ?? string.Empty;
        Title = track.Review.Title.Value ?? string.Empty;

        DanceSource = ReviewText.SourceOf(track.Entry.From(TrackField.Dance), track.Review.Dance);
        ArtistSource = ReviewText.SourceOf(track.Entry.From(TrackField.Artist), track.Review.Artist);
        TitleSource = ReviewText.SourceOf(track.Entry.From(TrackField.Title), track.Review.Title);

        ReasonText = ReviewText.ReasonOf(track.Review.Reason);

        AnswerFolderText = UiStrings.Review_ApproveFolder;
        IsInFolder = track.IsInFolder;
        ShowUnanswered();

        UnknownValue = track.UnknownValue;
        SharedBy = track.SharedBy;
        IsShared = track.SharedBy > 1;
        SharedText = IsShared
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.Review_UseForAll, track.SharedBy, track.UnknownValue)
            : string.Empty;
        NotADanceText = string.Format(CultureInfo.CurrentCulture, UiStrings.Review_NotADance, track.UnknownValue);
        HasUnknownValue = track.UnknownValue.Length > 0;
        Suggestions = track.Suggestions;
        HasSuggestions = track.Suggestions.Count > 0;
    }

    public ReviewTrack Track { get; }

    public string Path => Track.Path;

    /// <summary>The folder this sits in, which is the group it can be confirmed with.</summary>
    public string Folder => Track.Folder;

    /// <summary>True for the row that carries the folder's header, so a header is not a row of its own.</summary>
    public bool IsFirstOfGroup { get; }

    /// <summary>
    /// How many rows the folder button would answer, and the label saying so.
    /// </summary>
    /// <remarks>
    /// On the button rather than in a tooltip, because a library with everything in one directory is
    /// one group of two thousand, and "answer this folder" would then be a keystroke that answers
    /// the whole library without ever having said so.
    /// </remarks>
    [Reactive] public partial int AnswerableInFolder { get; set; }

    [Reactive] public partial string AnswerFolderText { get; set; }

    /// <summary>Whether it sits in a folder at all. The music directory itself is not one.</summary>
    public bool IsInFolder { get; private set; }

    /// <summary>Whether there is a folder here worth answering in one act.</summary>
    [Reactive] public partial bool CanAnswerFolder { get; set; }

    public string FileName { get; }

    public string FolderText { get; }

    [Reactive] public partial string Dance { get; set; }

    [Reactive] public partial string Artist { get; set; }

    [Reactive] public partial string Title { get; set; }

    public string DanceSource { get; }

    public string ArtistSource { get; }

    public string TitleSource { get; }

    public string ReasonText { get; }

    /// <summary>What this row is doing: why it is waiting, or that it has been answered.</summary>
    [Reactive] public partial string StatusText { get; private set; }

    /// <summary>
    /// True once it has been answered here.
    /// </summary>
    /// <remarks>
    /// The row stays exactly where it is. Removing it the moment it is answered leaves no way to see
    /// what was decided or to correct a mis-click, and makes every row below it jump under the
    /// pointer of somebody working through two thousand of them.
    /// </remarks>
    [Reactive] public partial bool IsApproved { get; private set; }

    /// <summary>True when the answer cannot let it into the library, so the row says why.</summary>
    [Reactive] public partial bool IsParked { get; set; }

    /// <summary>
    /// Where the row stands, for the eye rather than for the logic.
    /// </summary>
    /// <remarks>
    /// Answered rows stay in the list, so working down a folder has to be visible at a glance:
    /// green behind you, nothing ahead of you.
    /// </remarks>
    [Reactive] public partial ReviewRowState State { get; private set; }

    /// <summary>
    /// True while this is the track playing.
    /// </summary>
    /// <remarks>
    /// One at a time, and on the row itself: the fastest way to answer "which dance is this" is to
    /// hear eight seconds of it, and walking to a separate player to do that is the difference
    /// between answering forty rows and answering four.
    /// </remarks>
    [Reactive] public partial bool IsPreviewing { get; set; }

    /// <summary>The value this track claims that the list cannot answer, or empty.</summary>
    public string UnknownValue { get; }

    public int SharedBy { get; }

    /// <summary>True when other waiting tracks claim the same thing, so one answer settles them.</summary>
    public bool IsShared { get; }

    public bool HasUnknownValue { get; }

    public string SharedText { get; }

    public string NotADanceText { get; }

    /// <summary>What the unknown value might have meant. Offered, never applied.</summary>
    public IReadOnlyList<string> Suggestions { get; }

    public bool HasSuggestions { get; }

    /// <summary>Takes a suggestion, which is the same as having typed it.</summary>
    public void Take(string suggestion)
    {
        _taking = true;
        Dance = suggestion;
        _taking = false;

        ClosePicker();
    }

    /// <summary>
    /// True for a moment after this row refused to be answered.
    /// </summary>
    /// <remarks>
    /// A keystroke that does nothing reads as a keystroke that was not received, and the answer is
    /// to make the refusal visible rather than to explain it in a message nobody is looking at.
    /// </remarks>
    [Reactive] public partial bool IsRejected { get; private set; }

    /// <summary>How many times this row has been asked and said no.</summary>
    public int RejectedCount { get; private set; }

    /// <summary>
    /// Says no, visibly, however many times it is asked.
    /// </summary>
    /// <remarks>
    /// The animation runs off a class going on, so it has to come off and go back on across two
    /// passes of the dispatcher: setting the flag false and true in one breath is a single change
    /// as far as the binding is concerned, and the second refusal shows nothing at all. The pending
    /// reset is dropped as well, or the timer from the first press clears the second flash halfway
    /// through and leaves the row red until something else disturbs it.
    /// </remarks>
    public void Reject()
    {
        // Counted synchronously, because the flash itself runs on the dispatcher and a test has no
        // pump to run it on: what is worth asserting is that the row was told to say no.
        RejectedCount++;

        _flashReset?.Dispose();
        _flashReset = null;
        IsRejected = false;

        Dispatcher.UIThread.Post(
            () =>
            {
                IsRejected = true;
                _flashReset = Observable.Timer(TimeSpan.FromSeconds(1))
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Subscribe(_ => IsRejected = false);
            },
            DispatcherPriority.Background);
    }

    /// <summary>
    /// The names the list holds that match what has been typed, and the one the keys are on.
    /// </summary>
    /// <remarks>
    /// Ours rather than an AutoCompleteBox's, because that control's dropdown cannot be walked with
    /// the arrows: pressing down takes the first match and closes, which is no use for choosing
    /// between the four bourrées it just offered.
    /// </remarks>
    [Reactive] public partial IReadOnlyList<DanceMatch> DanceMatches { get; private set; }

    [Reactive] public partial bool IsPickerOpen { get; set; }

    /// <summary>The name the keys are on, or nothing when the list is closed.</summary>
    public string? HighlightedDance => DanceMatches.FirstOrDefault(match => match.IsHighlighted)?.Name;

    /// <summary>Recomputes what the list has to offer for the text as it now stands.</summary>
    public void ShowMatches()
    {
        if (_taking)
        {
            return;
        }

        (DanceMatches, IsPickerOpen) = DancePicking.MatchesFor(_allDances, Dance);
    }

    /// <summary>Walks the offered names, wrapping, which is the whole reason this list is ours.</summary>
    public void MoveHighlight(int direction)
    {
        DancePicking.MoveHighlight(DanceMatches, direction);
        this.RaisePropertyChanged(nameof(HighlightedDance));
    }

    /// <summary>Takes the highlighted name, which is what Enter means while the list is open.</summary>
    public bool TakeHighlighted()
    {
        if (HighlightedDance is not { } name)
        {
            return false;
        }

        _taking = true;
        Dance = name;
        _taking = false;

        ClosePicker();
        return true;
    }

    public void ClosePicker()
    {
        IsPickerOpen = false;
        DanceMatches = [];
    }

    public bool CanApprove =>
        !string.IsNullOrWhiteSpace(Dance)
        && !string.IsNullOrWhiteSpace(Artist)
        && !string.IsNullOrWhiteSpace(Title);

    /// <summary>Clears a value that turned out to be junk rather than an answer.</summary>
    public void ForgetUnknownValue()
    {
        if (string.Equals(Dance, UnknownValue, StringComparison.Ordinal))
        {
            Dance = string.Empty;
        }
    }

    public void MarkApproved(bool intoTheLibrary)
    {
        IsApproved = true;
        IsParked = !intoTheLibrary;
        State = intoTheLibrary ? ReviewRowState.Answered : ReviewRowState.Parked;
        // Short, because it shares a column with a button: the sentence explaining it lives under
        // the row, where there is width for one.
        StatusText = intoTheLibrary ? UiStrings.Review_Answered : UiStrings.Review_ParkedOnUnknownDance;
    }

    /// <summary>
    /// Takes the answer back, so the row is a question again.
    /// </summary>
    /// <remarks>
    /// The three values stay in their boxes, because a correction starts from what was answered
    /// rather than from a blank row. What it lands on is what the queue would draw for a track with
    /// no answer on it, park included: a row taken back has to read the same as the row a rebuild
    /// puts in its place, or the two disagree about whether it is still a question.
    /// </remarks>
    public void MarkWithdrawn()
    {
        IsApproved = false;
        ShowUnanswered();
    }

    /// <summary>
    /// Puts the row the way the queue draws a track nothing has been answered about.
    /// </summary>
    /// <remarks>
    /// Read off the review rather than remembered, and shared with the constructor: a row answered
    /// in an earlier sitting comes back through the queue rather than through
    /// <see cref="MarkApproved"/>, so a track parked on a dance the published list does not carry
    /// would otherwise look untouched every time it is opened.
    /// </remarks>
    private void ShowUnanswered()
    {
        IsParked = Track.Review.Reason is ReviewReason.UnknownDance;
        State = IsParked ? ReviewRowState.Parked : ReviewRowState.Waiting;
        StatusText = ReasonText;
    }
}

/// <summary>One name the published list offered for what has been typed.</summary>
/// <remarks>
/// A small object rather than a bare string, because the row that is highlighted has to say so
/// itself: a converter cannot be told which one it is without a binding as its parameter, and that
/// is the one thing a converter parameter may not be.
/// </remarks>
public sealed partial class DanceMatch(string name) : ReactiveObject
{
    public string Name { get; } = name;

    [Reactive] public partial bool IsHighlighted { get; set; }
}

/// <summary>What a row looks like: waiting, answered, or answered and held back.</summary>
public enum ReviewRowState
{
    Waiting,

    /// <summary>Answered and in the library.</summary>
    Answered,

    /// <summary>Answered, and waiting on a dance the published list does not carry yet.</summary>
    Parked
}

/// <summary>The words this screen puts on sources and reasons.</summary>
internal static class ReviewText
{
    /// <summary>Where a value came from, which is what makes a wrong source visible.</summary>
    public static string SourceOf(DerivedFrom from, ReviewedField field)
    {
        return field.ApprovedAs is ApprovalKind.Individual
            ? UiStrings.Review_FromYou
            : field.Rule is { } rule
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.Review_FromRule, rule)
            : from.Kind switch
            {
                ClaimSourceKind.Tag => string.Format(CultureInfo.CurrentCulture, UiStrings.Review_FromTag, from.Detail),
                ClaimSourceKind.FileName => string.Format(CultureInfo.CurrentCulture, UiStrings.Review_FromFileName, from.Detail),
                ClaimSourceKind.Folder => string.Format(CultureInfo.CurrentCulture, UiStrings.Review_FromFolder, from.Detail),
                _ => UiStrings.Review_FromNothing
            };
    }

    public static string ReasonOf(ReviewReason reason) => reason switch
    {
        ReviewReason.Missing => UiStrings.Review_ReasonMissing,
        ReviewReason.UnknownDance => UiStrings.Review_ReasonUnknownDance,
        ReviewReason.ChangedSinceApproval => UiStrings.Review_ReasonChanged,
        ReviewReason.Unapproved => UiStrings.Review_ReasonUnapproved,
        _ => string.Empty
    };
}
