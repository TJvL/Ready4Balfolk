using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Views.Dialogs.EditTrack;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class EditTrackDialogViewModelTests
{
    private readonly DanceListIndex _index = DanceListIndex.Build(TestData.CreateSimpleDanceList());

    private EditTrackDialogViewModel Build() => new(TestData.CreateTrack(), _index);

    [Fact]
    public void StartsWithTheTrackAsItIs_AndCanSave()
    {
        var sut = Build();

        Assert.Equal("Mazurka", sut.Dance);
        Assert.Equal("Artist", sut.Artist);
        Assert.Equal("Title", sut.Title);
        Assert.True(sut.CanSave);
        Assert.False(sut.HasProblem);
    }

    [Fact]
    public void ADanceTheListDoesNotKnow_IsRefusedAndPointsAtTheList()
    {
        // The list is the vocabulary. The fix for a missing dance is a proposal at BigBalfolkList,
        // and the refusal has to say so rather than leaving a dead save button.
        var sut = Build();

        sut.Dance = "Rond de Landéda";

        Assert.False(sut.CanSave);
        Assert.True(sut.HasProblem);
        Assert.Contains("BigBalfolkList", sut.Problem, StringComparison.Ordinal);
        Assert.Contains("Rond de Landéda", sut.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AnyNameTheListKnows_ResolvesToItsDisplaySpelling()
    {
        // "Schottische" is an alternate name; the saved value is the list's own spelling.
        var sut = Build();

        sut.Dance = "Schottische";

        Assert.True(sut.CanSave);
        Assert.Equal("Scottish", sut.ResolvedDance);
    }

    [Fact]
    public void AnEmptyArtistOrTitle_CannotBeSaved()
    {
        var sut = Build();

        sut.Artist = " ";

        Assert.False(sut.CanSave);
    }

    [Fact]
    public void TypingOpensThePicker_AndTakingAMatchClosesIt()
    {
        var sut = Build();

        sut.Dance = "sco";
        Assert.True(sut.IsPickerOpen);
        Assert.Contains(sut.DanceMatches, match => match.Name == "Scottish");

        Assert.True(sut.TakeHighlighted());
        Assert.False(sut.IsPickerOpen);
        Assert.True(sut.CanSave);
    }

    [Fact]
    public void TheLikelihood_StartsWhereTheTrackHasIt()
    {
        var sut = new EditTrackDialogViewModel(TestData.CreateTrack() with { Likelihood = 0.5 }, _index);

        Assert.Equal(0.5, sut.Likelihood);
        Assert.Equal("×½", sut.LikelihoodText);
    }

    [Theory]
    [InlineData(-2, 0.25, "×¼")]
    [InlineData(-1, 0.5, "×½")]
    [InlineData(0, 1, "×1")]
    [InlineData(1, 2, "×2")]
    [InlineData(2, 4, "×4")]
    public void EachStepOfTheSlider_IsAWholeDoubling(double step, double multiplier, string reads)
    {
        // Doublings either side of x1, so x1/4 and x4 are equally far from the middle and the slider
        // can only land on a multiplier a DJ would say out loud.
        var sut = Build();

        sut.LikelihoodStep = step;

        Assert.Equal(multiplier, sut.Likelihood);
        Assert.Equal(reads, sut.LikelihoodText);
    }

    [Fact]
    public void ATrackWithoutItsOwn_StartsFromTheGlobalCurveAndSavesNothing()
    {
        // The room as it is being corrected tonight is a better start than flat. Looking at it is
        // not giving the track one, though.
        var global = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -3 };
        var sut = new EditTrackDialogViewModel(TestData.CreateTrack(), _index, global);

        Assert.False(sut.UseOwnEqualizer);
        Assert.Equal(-3, sut.EqualizerPreamp);
        Assert.Null(sut.EqualizerToSave);
    }

    [Fact]
    public void TickingOwnEqualizer_SavesTheCurveOnTheSliders()
    {
        var sut = Build();

        sut.UseOwnEqualizer = true;
        sut.EqualizerBands[0].Gain = 5;
        sut.EqualizerPreamp = -2;

        var saved = sut.EqualizerToSave;
        Assert.NotNull(saved);
        Assert.True(saved.Enabled);
        Assert.Equal(5, saved.BandGains[0]);
        Assert.Equal(-2, saved.PreampDecibels);
    }

    [Fact]
    public void UntickingOwnEqualizer_KeepsTheCurveSwitchedOff()
    {
        var own = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -4 };
        var sut = new EditTrackDialogViewModel(TestData.CreateTrack() with { Equalizer = own }, _index);

        Assert.True(sut.UseOwnEqualizer);
        sut.UseOwnEqualizer = false;

        Assert.Equal(own with { Enabled = false }, sut.EqualizerToSave);
    }

    [Fact]
    public void ACopiedCurve_PastesOntoAnotherTrackAndSwitchesItOn()
    {
        var from = new EditTrackDialogViewModel(
            TestData.CreateTrack() with
            {
                Equalizer = EqualizerSettings.Flat.WithBandGain(4, -7) with { Enabled = true, LowCutEnabled = true }
            },
            _index);
        var onto = Build();

        Assert.True(onto.PasteEqualizer(from.EqualizerJson));

        Assert.True(onto.UseOwnEqualizer);
        Assert.Equal(-7, onto.EqualizerBands[4].Gain);
        Assert.True(onto.EqualizerLowCutEnabled);
        Assert.False(onto.HasEqualizerProblem);
    }

    [Fact]
    public void PastingSomethingThatIsNotACurve_IsRefusedAndChangesNothing()
    {
        var sut = Build();

        Assert.False(sut.PasteEqualizer("Naragonia - Salamandre"));

        Assert.True(sut.HasEqualizerProblem);
        Assert.False(sut.UseOwnEqualizer);
        Assert.Null(sut.EqualizerToSave);
    }

    [Fact]
    public void ASliderLeftBetweenSteps_LandsOnTheNearestOne()
    {
        var sut = Build();

        sut.LikelihoodStep = 1.4;

        Assert.Equal(2, sut.Likelihood);
    }
}
