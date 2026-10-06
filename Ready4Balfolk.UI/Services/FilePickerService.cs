using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Ready4Balfolk.Domain;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Services;

/// <summary>The desktop's own pickers, asked through the window they belong to.</summary>
public sealed class FilePickerService : IFilePickerService
{
    private Window? _owner;

    /// <summary>The window a picker belongs to, handed over once the main window exists.</summary>
    public void SetOwner(Window owner) => _owner = owner;

    public async Task<string?> PickFileToOpenAsync(string title, FileKind kind)
    {
        if (_owner?.StorageProvider is not { } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = FiltersFor(kind)
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (_owner?.StorageProvider is not { } storage)
        {
            return null;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickWhereToSaveAsync(string title, string suggestedName, FileKind kind)
    {
        if (_owner?.StorageProvider is not { } storage)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = FiltersFor(kind)
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>What the picker offers to show, which is the only thing a kind decides.</summary>
    /// <remarks>
    /// Audio is whatever BASS loaded support for rather than a list written here, so a build whose
    /// FLAC plugin did not load does not offer files it cannot then play.
    ///
    /// Every filter is made when a picker opens rather than once for the life of the application:
    /// its name is read out in the language the application is in, and that can change while the
    /// application runs.
    /// </remarks>
    private static IReadOnlyList<FilePickerFileType>? FiltersFor(FileKind kind) => kind switch
    {
        FileKind.Json => [Named(UiStrings.Dialog_JsonFiles, "*.json", "application/json")],
        FileKind.Text => [Named(UiStrings.Dialog_TextFiles, "*.txt", "text/plain")],
        FileKind.Html => [Named(UiStrings.Dialog_HtmlFiles, "*.html", "text/html")],
        FileKind.Csv => [Named(UiStrings.Dialog_CsvFiles, "*.csv", "text/csv")],
        FileKind.Audio => [AudioFiles()],
        _ => null
    };

    private static FilePickerFileType Named(string name, string pattern, string mimeType) => new(name)
    {
        Patterns = [pattern],
        MimeTypes = [mimeType]
    };

    private static FilePickerFileType AudioFiles() => new(UiStrings.Settings_EndOfNightAudioFiles)
    {
        Patterns = SupportedAudioFormats.Extensions.Count > 0
            ? [.. SupportedAudioFormats.Extensions.Select(extension => $"*{extension}")]
            : ["*"]
    };
}
