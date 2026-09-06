using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;

namespace Ready4Balfolk.UI.Services;

public class ConfirmationService : IConfirmationService
{
    public void SetOwner(Window owner) => CurrentOwner = owner;

    /// <summary>
    /// The window a modal question belongs to, or null before there is one.
    /// </summary>
    /// <remarks>
    /// Read by anything else that puts up a dialog, so "which window owns this" is answered in one
    /// place rather than once per service. There is one window: the wizard and every other screen
    /// are controls inside it.
    /// </remarks>
    public Window? CurrentOwner { get; private set; }

    public async Task<bool> ConfirmAsync(string title, string message,
        string confirmText = "Yes", string cancelText = "No",
        ConfirmationStakes stakes = ConfirmationStakes.Destructive,
        CancellationToken cancellationToken = default)
    {
        var owner = CurrentOwner;
        if (owner is null)
        {
            return true;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var vm = new ConfirmationDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = confirmText,
            CancelText = cancelText,
            Stakes = stakes
        };
        var dialog = new ConfirmationDialogView
        {
            DataContext = vm
        };

        // A question left standing over a dance that has already ended has no right answer, so it
        // goes away with the dance rather than waiting to be answered wrongly.
        using var withdrawal = cancellationToken.Register(
            () => Dispatcher.UIThread.Post(() => dialog.Close()));

        await dialog.ShowDialog(owner);
        return !cancellationToken.IsCancellationRequested && vm.DialogResult == true;
    }
}
