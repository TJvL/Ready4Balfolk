using System;
using System.Threading.Tasks;
using Ready4Balfolk.UI.Views.Dialogs.Message;
using Ready4Balfolk.UI.Views.Dialogs.QrCode;

namespace Ready4Balfolk.UI.Services;

public sealed class DialogService(DialogOwner owner) : IDialogService
{
    public async Task<MessageRequest?> RequestMessageAsync()
    {
        if (owner.Current is not { } window)
        {
            return null;
        }

        var viewModel = new RequestMessageDialogViewModel();
        await new RequestMessageDialogView { DataContext = viewModel }.ShowDialog(window);

        return viewModel.DialogResult == true
            ? new MessageRequest(
                viewModel.Message,
                viewModel.UseDelay ? TimeSpan.FromSeconds((double)viewModel.DelaySeconds) : null)
            : null;
    }

    public async Task ShowAddressAsync(QrCodeDialogViewModel address)
    {
        if (owner.Current is { } window)
        {
            await new QrCodeDialogView { DataContext = address }.ShowDialog(window);
        }
    }
}
