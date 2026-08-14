using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Helpers;
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

    /// <summary>The name the keys are on, or nothing when the list is closed.</summary>
    public string? HighlightedDance => DanceMatches.FirstOrDefault(match => match.IsHighlighted)?.Name;

    public void MoveHighlight(int direction)
    {
        if (DanceMatches.Count == 0)
        {
            return;
        }

        var at = DanceMatches.ToList().FindIndex(match => match.IsHighlighted);
        var next = (at + direction + DanceMatches.Count) % DanceMatches.Count;

        for (var i = 0; i < DanceMatches.Count; i++)
        {
            DanceMatches[i].IsHighlighted = i == next;
        }

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

        var typed = StringNormalizer.Normalize(Dance);
        if (typed.Length == 0)
        {
            ClosePicker();
            return;
        }

        // Starting with what was typed first, then merely containing it: somebody typing "bou"
        // means bourrée, and the dances that only mention it belong underneath.
        var matches = _allDances
            .Select(name => (Name: name, Folded: StringNormalizer.Normalize(name)))
            .Where(candidate => candidate.Folded.Contains(typed, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Folded.StartsWith(typed, StringComparison.Ordinal))
            .ThenBy(candidate => candidate.Name, StringComparer.CurrentCulture)
            .Select(candidate => candidate.Name)
            .Take(12)
            .ToList();

        DanceMatches = [.. matches.Select((name, index) => new DanceMatch(name) { IsHighlighted = index == 0 })];

        // Nothing to choose between when the only match is what is already written.
        IsPickerOpen = matches.Count > 0
            && !(matches.Count == 1 && string.Equals(StringNormalizer.Normalize(matches[0]), typed, StringComparison.Ordinal));
    }

    private void Validate()
    {
        var known = ResolvedDance is not null;

        Problem = !known && !string.IsNullOrWhiteSpace(Dance)
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.EditTrack_UnknownDance, Dance.Trim())
            : string.Empty;

        CanSave = known
            && !string.IsNullOrWhiteSpace(Artist)
            && !string.IsNullOrWhiteSpace(Title);
    }
}
