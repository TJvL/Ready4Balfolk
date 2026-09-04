using Avalonia.Controls;

namespace Ready4Balfolk.UI.Controls;

/// <summary>Takes a tooltip down before a window opens over the button it belongs to.</summary>
/// <remarks>
/// A tooltip closes when the pointer leaves the control it is on. Open a modal from the click and
/// the pointer never leaves: the new window takes it, the button underneath is never told, and the
/// tooltip is still hanging there when the dialog is closed again, over a button nobody is pointing
/// at.
/// </remarks>
internal static class Tooltips
{
    /// <summary>Closes the tooltip of whatever was clicked, if it has one showing.</summary>
    public static void Dismiss(object? clicked)
    {
        if (clicked is Control control)
        {
            ToolTip.SetIsOpen(control, false);
        }
    }
}
