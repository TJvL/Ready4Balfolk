using Avalonia.Controls;

namespace Ready4Balfolk.UI.Services;

/// <summary>The window every dialog and picker belongs to, set once and read by all of them.</summary>
/// <remarks>
/// There is one window: the wizard and every other screen are controls inside it. Each service that
/// puts something up used to keep an owner of its own, set one by one at startup, and two views
/// asked their own top level instead, so "which window owns this" had five answers that could
/// drift apart.
/// </remarks>
public sealed class DialogOwner
{
    /// <summary>The main window, or null before there is one.</summary>
    public Window? Current { get; private set; }

    /// <summary>Handed the main window once it exists.</summary>
    public void Set(Window owner) => Current = owner;
}
