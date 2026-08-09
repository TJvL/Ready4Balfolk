using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's first step: what is about to happen, and nothing to answer.</summary>
/// <remarks>
/// Deliberately offers no choice. The first thing a new user sees should tell them what the next
/// few minutes consist of, not ask them to pick between two things they have no way to judge yet.
/// </remarks>
public sealed class WelcomeStepViewModel : WizardStepViewModel
{
    /// <summary>Where a ready-made dance list can be had. Opened in the user's own browser.</summary>
    public const string DanceListSourceUrl = "https://github.com/TJvL/BigBalfolkList";

    public override string Title => UiStrings.Wizard_Welcome_Title;

    public override string Explanation => UiStrings.Wizard_Welcome_Explanation;
}
