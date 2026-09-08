using Ready4Balfolk.UI.Views.Review;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The mapping from a row's state to the App.axaml brush key it should be painted with. A row's
/// colour is the only thing that tells someone scanning a long list where it stands, so a typo
/// that swapped two of the five keys here would compile, run, and quietly show the wrong colour.
/// </summary>
public sealed class ReviewStateBrushConverterTests
{
    [Theory]
    [InlineData(ReviewRowState.Answered, false, "ReviewAnsweredBrush")]
    [InlineData(ReviewRowState.Answered, true, "ReviewAnsweredFillBrush")]
    [InlineData(ReviewRowState.Parked, false, "ReviewParkedBrush")]
    [InlineData(ReviewRowState.Parked, true, "ReviewParkedFillBrush")]
    public void EachStateAndFillCombination_MapsToItsOwnKey(ReviewRowState state, bool filling, string expectedKey)
    {
        var key = ReviewStateBrushConverter.ResourceKeyFor(state, filling);

        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData(ReviewRowState.Waiting, false)]
    [InlineData(ReviewRowState.Waiting, true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public void AWaitingOrMissingState_HasNoKey(ReviewRowState? state, bool filling)
    {
        var key = ReviewStateBrushConverter.ResourceKeyFor(state, filling);

        Assert.Null(key);
    }
}
