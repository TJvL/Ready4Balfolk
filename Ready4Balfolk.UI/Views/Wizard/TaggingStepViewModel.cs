using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Tagging;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's last step: what the scan could not place.</summary>
/// <remarks>
/// Never blocks. Setup finishing does not depend on the library being perfectly tagged, and a wizard
/// that refused to close until 1465 files were answered would simply never be closed.
/// </remarks>
public sealed class TaggingStepViewModel(TaggingViewModel editor) : WizardStepViewModel
{
    /// <summary>The same editor the application uses everywhere else.</summary>
    public TaggingViewModel Editor { get; } = editor;

    public override string Title => UiStrings.Wizard_Tagging_Title;

    public override string Explanation => UiStrings.Wizard_Tagging_Explanation;

    public override Task EnterAsync() => Editor.RefreshCommand.Execute().FirstAsync().ToTask();
}
