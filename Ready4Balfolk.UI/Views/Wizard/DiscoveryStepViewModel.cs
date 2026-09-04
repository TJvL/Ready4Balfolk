using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Discovery;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>
/// The step where a user says how their files are named, before they answer any of them by hand.
/// </summary>
/// <remarks>
/// It sits here because this is where it pays: a rule declared now answers two thousand files in one
/// act, and the review step after it is then the leftovers rather than the whole library. One of the
/// four ways of reading a library has to be ticked, because a library nothing is read by is a review
/// screen with every file in it.
/// </remarks>
public sealed class DiscoveryStepViewModel(DiscoveryViewModel discovery) : WizardStepViewModel
{
    /// <summary>The same screen the settings keep afterwards.</summary>
    public DiscoveryViewModel Discovery { get; } = discovery;

    public override string Title => UiStrings.Wizard_Discovery_Title;

    public override string Explanation => UiStrings.Wizard_Discovery_Explanation;

    /// <summary>
    /// One section ticked is enough. Which one, and what is in it, is theirs.
    /// </summary>
    /// <remarks>
    /// Ticked rather than filled in: a person who says their folders are the dance and then sets
    /// every level to unknown has said something about their library, and second-guessing that here
    /// would be the application deciding what a good declaration looks like.
    /// </remarks>
    public override IObservable<bool> CanContinue =>
        Discovery.WhenAnyValue(
            x => x.UsesFileNamePatterns,
            x => x.UsesFolderRoles,
            x => x.UsesTagTrust,
            x => x.UsesCustomDanceTag,
            (names, folders, tags, custom) => names || folders || tags || custom);

    public override string BlockedReason => UiStrings.Wizard_Discovery_Required;

    public override Task EnterAsync() => Discovery.RefreshCommand.Execute().FirstAsync().ToTask();

    /// <summary>
    /// Next is what saves the folder levels and the tags here.
    /// </summary>
    /// <remarks>
    /// The screen keeps a save button of its own for the settings, where nothing else would commit
    /// it. Inside a wizard that button is a second way to do what the continue button already means,
    /// and two buttons that both look like the way forward is how a step gets left half applied.
    /// </remarks>
    public override async Task<bool> CommitAsync()
    {
        await Discovery.ApplyRolesAndTagsCommand.Execute().FirstAsync().ToTask();
        return true;
    }
}
