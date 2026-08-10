using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Discovery;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>
/// The step where a user says how their files are named, before they answer any of them by hand.
/// </summary>
/// <remarks>
/// It sits here because this is where it pays: a rule declared now answers two thousand files in one
/// act, and the review step after it is then the leftovers rather than the whole library. Optional,
/// like every other step: a library with no shape to declare skips straight past it.
/// </remarks>
public sealed class DiscoveryStepViewModel(DiscoveryViewModel discovery) : WizardStepViewModel
{
    /// <summary>The same screen the settings keep afterwards.</summary>
    public DiscoveryViewModel Discovery { get; } = discovery;

    public override string Title => UiStrings.Wizard_Discovery_Title;

    public override string Explanation => UiStrings.Wizard_Discovery_Explanation;

    public override Task EnterAsync() => Discovery.RefreshCommand.Execute().FirstAsync().ToTask();
}
