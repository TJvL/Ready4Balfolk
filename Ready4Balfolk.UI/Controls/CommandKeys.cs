using System.Windows.Input;

namespace Ready4Balfolk.UI.Controls;

/// <summary>A key standing in for a button, which asks the button's command first.</summary>
public static class CommandKeys
{
    /// <summary>Runs the command, or nothing when it says it cannot run.</summary>
    /// <remarks>
    /// Asked rather than told. A command refuses for a reason, such as no audio to act on or a
    /// selected entry that cannot move, and a keystroke that arrives while it refuses should do
    /// nothing at all rather than be pushed through a command that has already said no. Clicking
    /// the button is held to the same answer, because the button is disabled.
    /// </remarks>
    public static void Press(ICommand command)
    {
        if (command.CanExecute(parameter: null))
        {
            command.Execute(parameter: null);
        }
    }
}
