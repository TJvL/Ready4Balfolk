using System;
using System.Threading.Tasks;
using Ready4Balfolk.UI.Views.Dialogs.QrCode;

namespace Ready4Balfolk.UI.Services;

/// <summary>What the DJ typed into the message dialog, and for how long it should stay up.</summary>
/// <param name="Message">The text of the announcement.</param>
/// <param name="Duration">How long it holds the room, or null for until somebody moves on.</param>
public sealed record MessageRequest(string Message, TimeSpan? Duration);

/// <summary>The dialogs the toolbars put up, shown through the one owner window.</summary>
public interface IDialogService
{
    /// <summary>Asks for a message to queue, or null when the DJ thought better of it.</summary>
    Task<MessageRequest?> RequestMessageAsync();

    /// <summary>Puts an address on screen as a code a phone can be pointed at.</summary>
    Task ShowAddressAsync(QrCodeDialogViewModel address);
}
