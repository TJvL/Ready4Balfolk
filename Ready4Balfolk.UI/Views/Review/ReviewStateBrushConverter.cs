using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>Marks how far a row has got, in the one way that reads across a long list.</summary>
/// <remarks>
/// Answered rows stay where they are rather than disappearing, so the colour is what tells somebody
/// working down a folder where they are. Amber rather than green for a parked one: it has been
/// answered and it is still not in the library, and reading those two states the same is how a
/// track goes missing without anybody noticing.
/// </remarks>
public sealed class ReviewStateBrushConverter : IValueConverter
{
    public static readonly ReviewStateBrushConverter Instance = new();

    private static readonly IBrush Answered = Brushes.MediumSeaGreen;
    private static readonly IBrush Parked = Brushes.Goldenrod;
    private static readonly IBrush Waiting = Brushes.Transparent;

    /// <summary>The whole row is filled, because a bar down its edge is not something you see.</summary>
    private static readonly IBrush AnsweredFill = new SolidColorBrush(Colors.MediumSeaGreen, 0.28);
    private static readonly IBrush ParkedFill = new SolidColorBrush(Colors.Goldenrod, 0.28);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var filling = string.Equals(parameter as string, "fill", StringComparison.Ordinal);

        return value switch
        {
            ReviewRowState.Answered => filling ? AnsweredFill : Answered,
            ReviewRowState.Parked => filling ? ParkedFill : Parked,
            _ => Waiting
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
