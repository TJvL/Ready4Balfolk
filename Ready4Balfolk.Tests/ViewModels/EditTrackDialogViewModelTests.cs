using Ready4Balfolk.Domain.Models.Dances;
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
    public void ASliderLeftBetweenSteps_LandsOnTheNearestOne()
    {
        var sut = Build();

        sut.LikelihoodStep = 1.4;

        Assert.Equal(2, sut.Likelihood);
    }
}
