using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Review;

namespace Ready4Balfolk.UI.Views.Dialogs.EditTrack;

/// <summary>
/// Fixes one library track's fields the moment a typo is spotted, without leaving the catalog.
/// </summary>
/// <remarks>
/// The track never goes back through review: it stays in the library and the pool throughout, and
/// what is saved is an individual approval of each changed field. The dance is still the published
/// list's to hand out, so a name the list does not know is refused here; the real fix for one is a
/// proposal at BigBalfolkList, not a local override.
/// </remarks>
public sealed class EditTrackDialogViewModel : ReactiveObject
{
    private readonly DanceListIndex _index;
    private readonly IReadOnlyList<string> _allDances;
    private readonly string _originalDance;
    private bool _taking;

    public EditTrackDialogViewModel(Track track, DanceListIndex index)
    {
        _index = index;
        _allDances =
        [
            .. index.Dances
                .Select(dance => dance.DisplayName)
                .OrderBy(name => name, StringComparer.CurrentCulture)
        ];

        _originalDance = track.Dance;
        Dance = track.Dance;
        Artist = track.Artist;
        Title = track.Title;

        var canSave = this.WhenAnyValue(x => x.CanSave);
        SaveCommand = ReactiveCommand.Create(() => DialogResult = true, canSave);
        CancelCommand = ReactiveCommand.Create(() => DialogResult = false);
        TakeCommand = ReactiveCommand.Create<string>(name => Take(name));

        this.WhenAnyValue(x => x.Dance, x => x.Artist, x => x.Title)
            .Subscribe(_ =>
            {
                Validate();
                ShowMatches();
            });

        // Not while the picker is open: half a name is a choice in progress, and refusing it
        // under the very completion being offered reads as the dialog contradicting itself.
        this.WhenAnyValue(x => x.Problem, x => x.IsPickerOpen, (problem, open) => problem.Length > 0 && !open)
            .Subscribe(show => HasProblem = show);
    }

    public string Dance
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Artist
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Title
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public IReadOnlyList<DanceMatch> DanceMatches
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsPickerOpen
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Why saving is refused, or empty. The one refusal is a dance the list lacks.</summary>
    public string Problem
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool HasProblem
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanSave
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool? DialogResult
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand TakeCommand { get; }

    /// <summary>The list's own spelling of what was typed, or null while it knows no such dance.</summary>
    public string? ResolvedDance =>
        _index.ResolveSlug(Dance) is { } slug ? _index.DisplayNameFor(slug) : null;

    /// <summary>What saving writes as the dance, or null while the dialog refuses.</summary>
    /// <remarks>
    /// The track's own dance survives even when the list does not know it: a track let in through
    /// the outside-the-list door must stay editable on its other fields, so only a NEW unknown
    /// name is refused. Leaving the dance alone is not a claim the list has to vouch for.
    /// </remarks>
    public string? DanceToSave =>
        ResolvedDance ?? (DanceIsUntouched ? _originalDance : null);

    private bool DanceIsUntouched => string.Equals(Dance.Trim(), _originalDance, StringComparison.Ordinal);

    /// <summary>The name the keys are on, or nothing when the list is closed.</summary>
    public string? HighlightedDance => DanceMatches.FirstOrDefault(match => match.IsHighlighted)?.Name;

    public void MoveHighlight(int direction)
    {
        DancePicking.MoveHighlight(DanceMatches, direction);
        this.RaisePropertyChanged(nameof(HighlightedDance));
    }

    public bool TakeHighlighted() => HighlightedDance is { } name && Take(name);

    public bool Take(string name)
    {
        _taking = true;
        try
        {
            Dance = name;
        }
        finally
        {
            _taking = false;
        }

        ClosePicker();
        return true;
    }

    public void ClosePicker()
    {
        DanceMatches = [];
        IsPickerOpen = false;
    }

    private void ShowMatches()
    {
        if (_taking)
        {
            return;
        }

        (DanceMatches, IsPickerOpen) = DancePicking.MatchesFor(_allDances, Dance);
    }

    private void Validate()
    {
        var acceptable = DanceToSave is not null;

        Problem = !acceptable && !string.IsNullOrWhiteSpace(Dance)
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.EditTrack_UnknownDance, Dance.Trim())
            : string.Empty;

        CanSave = acceptable
            && !string.IsNullOrWhiteSpace(Artist)
            && !string.IsNullOrWhiteSpace(Title);
    }
}
