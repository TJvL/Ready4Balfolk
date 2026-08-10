using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Review;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>The wizard's last step: everything the scan could not answer on its own.</summary>
/// <remarks>
/// Never blocks. Setup finishing does not depend on the library being answered, and a wizard that
/// refused to close until 1465 tracks had been agreed to would simply never be closed. It is the
/// same screen the application keeps afterwards, because this is not a phase of setup: files change,
/// and they come back here for as long as the library exists.
/// </remarks>
public sealed class ReviewStepViewModel(ReviewViewModel review) : WizardStepViewModel
{
    /// <summary>The same queue the application uses everywhere else.</summary>
    public ReviewViewModel Review { get; } = review;

    public override string Title => UiStrings.Wizard_Review_Title;

    public override string Explanation => UiStrings.Wizard_Review_Explanation;

    public override Task EnterAsync() => Review.RefreshCommand.Execute().FirstAsync().ToTask();
}
