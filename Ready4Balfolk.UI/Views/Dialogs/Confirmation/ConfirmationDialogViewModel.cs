using System.Windows.Input;
using ReactiveUI;

namespace Ready4Balfolk.UI.Views.Dialogs.Confirmation;

public class ConfirmationDialogViewModel : ReactiveObject
{
    public string Title
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Confirm";

    public string Message
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Are you sure?";

    public string ConfirmText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Yes";

    public string CancelText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "No";

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
