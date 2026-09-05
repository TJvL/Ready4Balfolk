using System.Windows.Input;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Dialogs.Confirmation;

public class ConfirmationDialogViewModel : ReactiveObject
{
    public string Title
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = UiStrings.Dialog_ConfirmDefault;

    public string Message
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = UiStrings.Dialog_AreYouSure;

    public string ConfirmText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = UiStrings.Dialog_YesDefault;

    public string CancelText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = UiStrings.Dialog_NoDefault;

    /// <summary>What confirming costs. Set once, when the question is asked.</summary>
    public ConfirmationStakes Stakes { get; init; } = ConfirmationStakes.Destructive;

    /// <summary>
    /// True when confirming is the answer a reflex should land on, which is the one that gets the
    /// return key, the focus on open and the accent.
    /// </summary>
    public bool ConfirmIsSafe => Stakes == ConfirmationStakes.Reversible;

    public bool? DialogResult
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public ConfirmationDialogViewModel()
    {
        ConfirmCommand = ReactiveCommand.Create(() => DialogResult = true);
        CancelCommand = ReactiveCommand.Create(() => DialogResult = false);
    }
}
