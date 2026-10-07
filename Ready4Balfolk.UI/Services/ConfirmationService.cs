using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;

namespace Ready4Balfolk.UI.Services;

public class ConfirmationService : IConfirmationService
{
    public void SetOwner(Window owner) => CurrentOwner = owner;

    /// <summary>
    /// The window a modal question belongs to, or null before there is one.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="MissingFolderPromptService"/> rather than set a second time. The file
    /// pickers and the track editor still keep an owner of their own (#310). There is one window:
    /// the wizard and every other screen are controls inside it.
    /// </remarks>
    public Window? CurrentOwner { get; private set; }

    public async Task<bool> ConfirmAsync(string title, string message,
        string? confirmText = null, string? cancelText = null,
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

        await dialog.ShowDialog(owner);
        return !cancellationToken.IsCancellationRequested && vm.DialogResult == true;
    }

    /// <summary>What the confirm button says when nobody named it: "Yes" in the current language.</summary>
    internal static string ResolveConfirmText(string? confirmText) => confirmText ?? UiStrings.Dialog_YesDefault;

    /// <summary>What the cancel button says when nobody named it: "No" in the current language.</summary>
    internal static string ResolveCancelText(string? cancelText) => cancelText ?? UiStrings.Dialog_NoDefault;
}
