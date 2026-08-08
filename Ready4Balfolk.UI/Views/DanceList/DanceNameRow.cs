namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>One spelling of the selected dance, with what can be done to it.</summary>
/// <remarks>
/// The names are a flat set of equals, so nothing here calls one of them canonical. The first is
/// simply the one being displayed, and moving another to the front is how that is changed.
/// </remarks>
public sealed record DanceNameRow(string Name, int Index, bool IsDisplayed, bool IsOnlyName)
{
    public bool CanMoveUp => Index > 0;

    public bool CanRemove => !IsOnlyName;
}
