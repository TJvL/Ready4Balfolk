using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Stores;

public class ApplicationSettingsDirectory(IFileSystem fileSystem) : IApplicationSettingsDirectory
{
    // SpecialFolderOption.Create, because without it GetFolderPath hands back an empty string on a
    // profile that has no XDG data directory yet. Path.Combine then yields a relative path and the
    // app quietly keeps its settings next to the executable, or, if that is read-only, dies on
    // startup while creating the log directory.
    public IDirectoryInfo DirectoryInfoRoot { get; } = fileSystem.DirectoryInfo.New(
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create),
            "Ready4Balfolk"));
}
