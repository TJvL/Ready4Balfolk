using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.DanceList;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's second step: the dance list editor, on the list just built.</summary>
/// <remarks>
/// An imported list is somebody else's answer, and it arrives with names this user does not use and
/// dances they never play. Editing it belongs here rather than only afterwards, while it is still
/// obvious that the list is the thing being set up.
/// </remarks>
public sealed class DanceListEditStepViewModel(DanceListViewModel editor) : WizardStepViewModel
{
    /// <summary>The same editor the application uses everywhere else.</summary>
    public DanceListViewModel Editor { get; } = editor;

    public override string Title => UiStrings.Wizard_DanceListEdit_Title;

    public override string Explanation => UiStrings.Wizard_DanceListEdit_Explanation;
}
