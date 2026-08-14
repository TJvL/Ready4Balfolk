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
}
