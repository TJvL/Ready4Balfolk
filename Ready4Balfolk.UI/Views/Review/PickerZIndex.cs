using Avalonia.Data.Converters;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>
/// Raises a row above its neighbours while its dance picker is open, because the picker paints
/// over the rows beneath instead of pushing them down: growing the row resized the virtualized
/// list and threw the viewport somewhere else, which cost the person the row they were answering.
/// </summary>
public static class PickerZIndex
{
    public static readonly IValueConverter Instance = new FuncValueConverter<bool, int>(open => open ? 100 : 0);
}
