using System;
using System.Collections.Generic;
using System.Globalization;
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
    public ReviewRowViewModel(ReviewTrack track, bool isFirstOfGroup)
    {
        Track = track;
        IsFirstOfGroup = isFirstOfGroup;
        FileName = track.FileName;
        FolderText = track.Folder.Length == 0 ? UiStrings.Review_RootFolder : track.Folder;

        Dance = track.Review.Dance.Value ?? string.Empty;
        Artist = track.Review.Artist.Value ?? string.Empty;
        Title = track.Review.Title.Value ?? string.Empty;

        DanceSource = ReviewText.SourceOf(track.Entry.From(TrackField.Dance), track.Review.Dance);
        ArtistSource = ReviewText.SourceOf(track.Entry.From(TrackField.Artist), track.Review.Artist);
        TitleSource = ReviewText.SourceOf(track.Entry.From(TrackField.Title), track.Review.Title);

        ReasonText = ReviewText.ReasonOf(track.Review.Reason);
        StatusText = ReasonText;

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
    [Reactive] public partial bool IsParked { get; private set; }

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
    public void Take(string suggestion) => Dance = suggestion;

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
        StatusText = intoTheLibrary
            ? UiStrings.Review_Answered
            : string.Format(CultureInfo.CurrentCulture, UiStrings.Review_ParkedOnUnknownDance, Dance);
    }
}

/// <summary>The words this screen puts on sources and reasons.</summary>
internal static class ReviewText
{
    /// <summary>Where a value came from, which is what makes a wrong source visible.</summary>
    public static string SourceOf(DerivedFrom from, ReviewedField field)
    {
        if (field.ApprovedAs is ApprovalKind.Individual)
        {
            return UiStrings.Review_FromYou;
        }

        if (field.Rule is { } rule)
        {
            return string.Format(CultureInfo.CurrentCulture, UiStrings.Review_FromRule, rule);
        }

        return from.Kind switch
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
