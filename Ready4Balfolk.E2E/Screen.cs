using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Ready4Balfolk.E2E;

/// <summary>What is on screen, found the way a person finds it.</summary>
/// <remarks>
/// Everything is looked up by automation id. Matching on a control's label instead would tie every
/// scenario to the English copy, and this application ships in two languages; matching on position
/// in the tree would tie them to the layout, which is the thing most likely to be rearranged.
/// </remarks>
internal static class Screen
{
    /// <summary>Every control with this automation id that is currently on screen.</summary>
    public static IEnumerable<Control> AllWith(Visual root, string automationId) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => string.Equals(
                AutomationProperties.GetAutomationId(control), automationId, StringComparison.Ordinal));

    /// <summary>What a control says: the text it draws, in the order it draws it.</summary>
    /// <remarks>
    /// The drawn text, never the object behind it. A row of a list is a ContentControl whose content
    /// is a view model, and asking that for a string gets its type name back: a scenario looking for
    /// a track in the history would then be told the history is full of
    /// <c>HistoryItemViewModel</c>, which is true and no use to anybody.
    /// </remarks>
    public static string Says(Control control)
    {
        switch (control)
        {
            case TextBlock text:
                return text.Text ?? string.Empty;
            case TextBox box:
                return box.Text ?? string.Empty;
            default:
                var drawn = control.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(text => text.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();

                return drawn.Count > 0
                    ? string.Join(" ", drawn)
                    : string.Empty;
        }
    }

    /// <summary>The lines a list is showing, one string per row, in the order they appear.</summary>
    public static IReadOnlyList<string> Rows(Control list) =>
        list.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Select(Says)
            .Where(row => !string.IsNullOrWhiteSpace(row))
            .ToList();

    /// <summary>The rows a data grid is showing, one string per row.</summary>
    /// <remarks>
    /// One per row index. The grid keeps a spare row around to recycle and it carries a copy of a
    /// real row's text, so a library of two tracks is three rows in the tree, two of them saying
    /// the same thing.
    /// </remarks>
    public static IReadOnlyList<string> GridRows(Control grid) =>
        grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => row.IsEffectivelyVisible)
            .OrderBy(row => row.Index)
            .DistinctBy(row => row.Index)
            .Select(Says)
            .Where(row => !string.IsNullOrWhiteSpace(row))
            .ToList();
}
