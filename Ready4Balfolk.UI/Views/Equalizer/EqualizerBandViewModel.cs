using System.Globalization;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace Ready4Balfolk.UI.Views.Equalizer;

/// <summary>
/// One slider of the graphic equalizer. The centre frequency is fixed, so only the gain is bound.
/// </summary>
public sealed partial class EqualizerBandViewModel(int centerFrequency) : ReactiveObject
{
    public int CenterFrequency { get; } = centerFrequency;

    /// <summary>Short form for the axis, so 6300 reads as "6k3" rather than overflowing the column.</summary>
    public string Label { get; } = FormatLabel(centerFrequency);

    [Reactive] public partial double Gain { get; set; }

    private static string FormatLabel(int frequency)
    {
        if (frequency < 1000)
        {
            return frequency.ToString(CultureInfo.InvariantCulture);
        }

        var thousands = frequency / 1000;
        var hundreds = frequency % 1000 / 100;

        return hundreds == 0 ? $"{thousands}k" : $"{thousands}k{hundreds}";
    }
}
