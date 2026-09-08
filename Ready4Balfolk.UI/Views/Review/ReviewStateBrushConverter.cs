using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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

    private static readonly IBrush Waiting = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var filling = string.Equals(parameter as string, "fill", StringComparison.Ordinal);
        var key = ResourceKeyFor(value as ReviewRowState?, filling);

        return key != null
               && Application.Current!.TryFindResource(key, Application.Current?.ActualThemeVariant, out var resource)
            ? (IBrush)resource!
            : Waiting;
    }

    /// <summary>The App.axaml brush key for a row's state, or null for a row nobody has touched.</summary>
    internal static string? ResourceKeyFor(ReviewRowState? state, bool filling) => state switch
    {
        ReviewRowState.Answered => filling ? "ReviewAnsweredFillBrush" : "ReviewAnsweredBrush",
        ReviewRowState.Parked => filling ? "ReviewParkedFillBrush" : "ReviewParkedBrush",
        _ => null
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
