using System.Threading.Tasks;
using Avalonia.Controls;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;

namespace Ready4Balfolk.UI.Services;

public class ConfirmationService : IConfirmationService
{
    private Window? _owner;

    public void SetOwner(Window owner) => _owner = owner;

    public async Task<bool> ConfirmAsync(string title, string message,
        string confirmText = "Yes", string cancelText = "No")
    {
        if (_owner is null)
        {
            return true;
        }

        var vm = new ConfirmationDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText,
            CancelText = cancelText
        };
        var dialog = new ConfirmationDialogView
        {
            DataContext = vm
        };
        await dialog.ShowDialog(_owner);
        return vm.DialogResult == true;
    }
}
