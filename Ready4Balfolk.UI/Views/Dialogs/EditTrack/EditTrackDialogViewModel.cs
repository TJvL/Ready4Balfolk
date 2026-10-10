using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Equalizer;
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
    private readonly bool _hadEqualizer;
    private bool _taking;

    /// <param name="track">The track as the library holds it now.</param>
    /// <param name="index">The published list, which is what the dance is checked against.</param>
    /// <param name="globalEqualizer">
    /// Where a track that has never had an equalizer of its own starts from: the room as it is
    /// being corrected tonight, which is closer to what the track wants than a flat curve is.
    /// </param>
    public EditTrackDialogViewModel(Track track, DanceListIndex index, EqualizerSettings? globalEqualizer = null)
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
        LikelihoodStep = Math.Round(Math.Log2(track.Likelihood));

        _hadEqualizer = track.Equalizer is not null;
        EqualizerBands =
        [
            .. EqualizerSettings.BandCenterFrequencies.Select(center => new EqualizerBandViewModel(center))
        ];
        LoadCurve(track.Equalizer ?? globalEqualizer ?? EqualizerSettings.Flat);
        UseOwnEqualizer = track.HasOwnEqualizer;

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

    /// <summary>Where the likelihood slider sits: whole doublings either side of ×1.</summary>
    /// <remarks>
    /// The slider moves in powers of two rather than in the multiplier itself, so ×¼ and ×4 are as
    /// far from ×1 as each other, and ×1 sits in the middle where a DJ expects to find it.
    /// </remarks>
    public double LikelihoodStep
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, Math.Clamp(Math.Round(value), LowestStep, HighestStep));
            this.RaisePropertyChanged(nameof(Likelihood));
            this.RaisePropertyChanged(nameof(LikelihoodText));
        }
    }

    public static double LowestStep => Math.Log2(TrackLikelihood.Lowest);

    public static double HighestStep => Math.Log2(TrackLikelihood.Highest);

    /// <summary>The multiplier saving writes down.</summary>
    public double Likelihood => Math.Pow(2, LikelihoodStep);

    /// <summary>The multiplier as a DJ reads it: ×¼, ×½, ×1, ×2, ×4.</summary>
    public string LikelihoodText => LikelihoodStep switch
    {
        -2.0 => "×¼",
        -1.0 => "×½",
        _ => "×" + Likelihood.ToString("0", CultureInfo.CurrentCulture)
    };

    /// <summary>Whether the track plays through the curve below rather than through the global one.</summary>
    public bool UseOwnEqualizer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<EqualizerBandViewModel> EqualizerBands { get; }

    public double EqualizerPreamp
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool EqualizerLowCutEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double EqualizerLowCutHertz
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Why a paste was refused, or empty.</summary>
    public string EqualizerProblem
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasEqualizerProblem));
        }
    } = string.Empty;

    public bool HasEqualizerProblem => EqualizerProblem.Length > 0;

    /// <summary>The curve on the sliders as the text Copy puts on the clipboard.</summary>
    public string EqualizerJson => Curve().ToJson();

    /// <summary>What saving writes as the track's own equalizer, or null for none at all.</summary>
    /// <remarks>
    /// A track that never had one and is still not given one has nothing to keep. One that had one
    /// and is switched off keeps its curve, switched off, so ticking the box again finds it.
    /// </remarks>
    public EqualizerSettings? EqualizerToSave =>
        !_hadEqualizer && !UseOwnEqualizer
            ? null
            : Curve() with { Enabled = UseOwnEqualizer };

    /// <summary>Takes a curve off the clipboard's text and puts it on this track.</summary>
    /// <returns>False, with the reason shown, when the text is not equalizer settings.</returns>
    public bool PasteEqualizer(string? text)
    {
        if (EqualizerSettings.FromJson(text) is not { } pasted)
        {
            EqualizerProblem = UiStrings.EditTrack_PasteRefused;
            return false;
        }

        // Pasting a curve onto a track is asking for the track to play through it.
        LoadCurve(pasted);
        UseOwnEqualizer = true;
        EqualizerProblem = string.Empty;
        return true;
    }

    private EqualizerSettings Curve() => new()
    {
        Enabled = true,
        BandGains = EqualizerBands.Select(band => band.Gain).ToArray(),
        PreampDecibels = EqualizerPreamp,
        LowCutEnabled = EqualizerLowCutEnabled,
        LowCutHertz = EqualizerLowCutHertz
    };

    private void LoadCurve(EqualizerSettings curve)
    {
        for (var index = 0; index < EqualizerBands.Count; index++)
        {
            EqualizerBands[index].Gain = curve.BandGains[index];
        }

        EqualizerPreamp = curve.PreampDecibels;
        EqualizerLowCutEnabled = curve.LowCutEnabled;
        EqualizerLowCutHertz = curve.LowCutHertz;
    }

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
