using System.Threading.Tasks;

namespace Ready4Balfolk.UI.Services;

/// <summary>What kind of file is being asked for, which is all the filters ever say.</summary>
public enum FileKind
{
    /// <summary>Anything the machine will offer.</summary>
    Anything,

    /// <summary>A <c>dances.json</c>, or a night exported as one.</summary>
    Json,

    /// <summary>A log, which is plain text.</summary>
    Text,

    /// <summary>Something the application can play, which is whatever BASS loaded support for.</summary>
    Audio
}

/// <summary>Asks the person for a file or a folder, and hands back a path.</summary>
/// <remarks>
/// <para>
/// A picker belongs to the desktop rather than to the application, and every view used to reach for
/// it itself: six code-behind files each built their own filters, each turned what came back into a
/// path, and each handled the top level being missing.
/// </para>
/// <para>
/// It is also the one step of a scenario that cannot be driven. Avalonia's storage interfaces
/// cannot be implemented outside the framework, so with the picker inside the view there is no way
/// for a scenario to say which file the person chose. Behind this, there is.
/// </para>
/// </remarks>
public interface IFilePickerService
{
    /// <summary>A file the person already has, or null when they changed their mind.</summary>
    Task<string?> PickFileToOpenAsync(string title, FileKind kind);

    /// <summary>A folder, or null when they changed their mind.</summary>
    Task<string?> PickFolderAsync(string title);

    /// <summary>Where to write something, or null when they changed their mind.</summary>
    Task<string?> PickWhereToSaveAsync(string title, string suggestedName, FileKind kind);
}
