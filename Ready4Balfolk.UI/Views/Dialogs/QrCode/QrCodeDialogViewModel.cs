using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using QRCoder;
using ReactiveUI.Reactive;

namespace Ready4Balfolk.UI.Views.Dialogs.QrCode;

/// <summary>One served page, as something a phone can be pointed at.</summary>
/// <remarks>
/// The alternative is reading an address out across a hall and having somebody type it into a phone
/// in the dark, which is how a helper ends up on the wrong port with the wrong digit.
/// </remarks>
public sealed class QrCodeDialogViewModel : ReactiveObject
{
    /// <summary>Pixels per module. Big enough to scan from across a table, small enough to fit.</summary>
    private const int ModuleSize = 8;

    public QrCodeDialogViewModel(
        string title,
        string address,
        string? pin,
        IReadOnlyList<string> otherAddresses)
    {
        Title = title;
        Address = address;
        Pin = pin ?? string.Empty;
        HasPin = !string.IsNullOrEmpty(pin);

        // The addresses this machine has that are not the one on screen. A hall machine with wifi
        // and a cable has several, and a code nobody on that network can reach is worse than none.
        OtherAddresses = string.Join("    ", otherAddresses.Where(other => other != address));
        HasOtherAddresses = OtherAddresses.Length > 0;

        Image = Draw(address);
    }

    public string Title { get; }

    /// <summary>The address the code carries, written out for a camera that will not read it.</summary>
    public string Address { get; }

    public string Pin { get; }

    public bool HasPin { get; }

    public string OtherAddresses { get; }

    public bool HasOtherAddresses { get; }

    public Bitmap Image { get; }

    /// <summary>
    /// The code itself, drawn here rather than fetched.
    /// </summary>
    /// <remarks>
    /// The hall has no wifi, which is the whole reason the server is local: a code drawn by an
    /// online service would be a blank square at exactly the moment it is needed.
    /// </remarks>
    private static Bitmap Draw(string address)
    {
        using var generator = new QRCodeGenerator();

        // Medium correction: a screen is not a crumpled poster, and the smaller code scans faster.
        using var data = generator.CreateQrCode(address, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(ModuleSize);

        using var stream = new MemoryStream(png);
        return new Bitmap(stream);
    }
}
