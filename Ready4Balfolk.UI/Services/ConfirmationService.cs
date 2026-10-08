using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;

namespace Ready4Balfolk.UI.Services;

public class ConfirmationService(DialogOwner owner) : IConfirmationService
{
    public async Task<bool> ConfirmAsync(string title, string message,
        string? confirmText = null, string? cancelText = null,
        ConfirmationStakes stakes = ConfirmationStakes.Destructive,
        CancellationToken cancellationToken = default)
    {
        if (owner.Current is not { } window)
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
            ConfirmText = ResolveConfirmText(confirmText),
            CancelText = ResolveCancelText(cancelText),
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

        await dialog.ShowDialog(window);
        return !cancellationToken.IsCancellationRequested && vm.DialogResult == true;
    }

    /// <summary>What the confirm button says when nobody named it: "Yes" in the current language.</summary>
    internal static string ResolveConfirmText(string? confirmText) => confirmText ?? UiStrings.Dialog_YesDefault;

    /// <summary>What the cancel button says when nobody named it: "No" in the current language.</summary>
    internal static string ResolveCancelText(string? cancelText) => cancelText ?? UiStrings.Dialog_NoDefault;
}
