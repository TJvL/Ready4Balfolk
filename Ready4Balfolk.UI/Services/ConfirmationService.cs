using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using Ready4Balfolk.UI.Views.Dialogs.Confirmation;

namespace Ready4Balfolk.UI.Services;

public class ConfirmationService : IConfirmationService
{
    private Window? _owner;
    private Window? _temporaryOwner;

    public void SetOwner(Window owner) => _owner = owner;

    /// <summary>
    /// The window a modal question belongs to right now, or null before there is one.
    /// </summary>
    /// <remarks>
    /// Read by anything else that puts up a dialog, so "which window owns this" is answered in one
    /// place: a second service tracking its own owner would miss <see cref="UseOwner"/> and parent
    /// a question raised from the wizard on a window the user cannot reach.
    /// </remarks>
    public Window? CurrentOwner => _temporaryOwner ?? _owner;

    /// <summary>
    /// Parents confirmations on another window until the returned handle is disposed.
    /// </summary>
    /// <remarks>
    /// The setup wizard is modal over the main window, so a confirmation raised from inside it must
    /// belong to the wizard. Parented to the main window it is owned by a window the user cannot
    /// reach, which reads as a button that did nothing.
    /// </remarks>
    public IDisposable UseOwner(Window owner)
    {
        var previous = _temporaryOwner;
        _temporaryOwner = owner;
        return Disposable.Create(() => _temporaryOwner = previous);
    }

    public async Task<bool> ConfirmAsync(string title, string message,
        string confirmText = "Yes", string cancelText = "No")
    {
        var owner = CurrentOwner;
        if (owner is null)
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
        await dialog.ShowDialog(owner);
        return vm.DialogResult == true;
    }
}
