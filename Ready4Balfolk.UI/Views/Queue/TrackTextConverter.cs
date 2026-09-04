using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Presentation;

namespace Ready4Balfolk.UI.Views.Queue;

/// <summary>Writes a track the way the template says, for the rows that are domain items.</summary>
/// <remarks>
/// A queue row is bound to the queue item itself rather than to a view model of its own, so the
/// template is handed in beside the track rather than read from anywhere: the converter keeps no
/// state, and a template edited in the settings reaches every row through the binding it already
/// has.
/// </remarks>
public sealed class TrackTextConverter : IMultiValueConverter
{
    public static readonly TrackTextConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values.Count == 2 && values[0] is Track track && values[1] is string template
            ? TrackTextTemplate.Render(template, track)
            : string.Empty;
    }
}
