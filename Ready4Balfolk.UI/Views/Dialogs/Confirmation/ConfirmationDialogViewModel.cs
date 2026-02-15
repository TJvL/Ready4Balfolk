using System.Windows.Input;
using ReactiveUI;
using Ready4Balfolk.UI.Resources;

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
