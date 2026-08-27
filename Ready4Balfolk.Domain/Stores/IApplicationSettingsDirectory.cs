using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Stores;

public interface IApplicationSettingsDirectory
{
    /// <summary>Where the application keeps everything it writes.</summary>
    /// <remarks>
    /// <see cref="IDirectoryInfo"/> rather than <see cref="DirectoryInfo"/>: the stores hanging off
    /// this were reachable only through the real filesystem, which is why the settings store had no
    /// tests. The SQLite stores still need a path that really exists, since SQLite opens the file
    /// itself, so in their tests this points at a temp directory rather than at a mock.
    /// </remarks>
    IDirectoryInfo DirectoryInfoRoot { get; }
}
