using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>One page of the setup wizard.</summary>
/// <remarks>
/// The wizard gains pages as the metadata work lands, so a step owns everything about itself: what
/// it is called, what it explains, whether the user may move on, and what committing it means.
/// </remarks>
public abstract class WizardStepViewModel : ReactiveObject
{
    /// <summary>Heading for the step.</summary>
    public abstract string Title { get; }

    /// <summary>What this step is for, in the user's terms.</summary>
    public abstract string Explanation { get; }

    /// <summary>Whether the wizard may move past this step yet.</summary>
    public virtual IObservable<bool> CanContinue => Observable.Return(true);

    /// <summary>Why the wizard will not move on, shown next to a disabled continue button.</summary>
    /// <remarks>
    /// A disabled button with no reason beside it is the most common way to strand a user. An
    /// observable like the verdict it explains, because a step can be blocked for one reason and
    /// then another: reading the library first, and what the person has not said yet after that.
    /// </remarks>
    public virtual IObservable<string> BlockedReason => Observable.Return(string.Empty);

    /// <summary>Whether Enter on this step means "continue".</summary>
    /// <remarks>
    /// False on a step whose own screen answers with Enter. A default button listens on the window,
    /// so it takes the keystroke every time the caret is not somewhere that claimed it first, and
    /// the step that reads that way is the review queue: one press per row, for as long as the
    /// sitting lasts, on the step whose Continue button says Finish.
    /// </remarks>
    public virtual bool EnterContinues => true;

    /// <summary>Runs when the step is shown, so it can pick up state a previous step wrote.</summary>
    public virtual Task EnterAsync() => Task.CompletedTask;

    /// <summary>Persists the step's answer. Returning false keeps the wizard on this step.</summary>
    public virtual Task<bool> CommitAsync() => Task.FromResult(true);
}
