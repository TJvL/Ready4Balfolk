using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class ConfirmationDialogViewModelTests
{
    [Fact]
    public void AQuestionThatThrowsSomethingAway_KeepsReturnOffTheYes()
    {
        var sut = new ConfirmationDialogViewModel
        {
            Stakes = ConfirmationStakes.Destructive
        };

        Assert.False(sut.ConfirmIsSafe);
    }

    [Fact]
    public void AQuestionThatSaysNothing_IsTreatedAsDestructive()
    {
        // A question added later without thinking about it must land on the safe side, not on the
        // one that clears the queue because return was already on its way down.
        var sut = new ConfirmationDialogViewModel();

        Assert.False(sut.ConfirmIsSafe);
    }

    [Fact]
    public void AQuestionThatTakesNothingAway_LeavesYesUnderTheReturnKey()
    {
        var sut = new ConfirmationDialogViewModel
        {
            Stakes = ConfirmationStakes.Reversible
        };

        Assert.True(sut.ConfirmIsSafe);
    }
}
