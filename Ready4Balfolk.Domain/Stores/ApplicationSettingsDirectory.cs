namespace Ready4Balfolk.Domain.Stores;

public class ApplicationSettingsDirectory : IApplicationSettingsDirectory
{
    // SpecialFolderOption.Create, because without it GetFolderPath hands back an empty string on a
    // profile that has no XDG data directory yet. Path.Combine then yields a relative path and the
    // app quietly keeps its settings next to the executable, or, if that is read-only, dies on
    // startup while creating the log directory.
    private static readonly DirectoryInfo DataDirectory =
        new(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create),
            "Ready4Balfolk"));

    public DirectoryInfo DirectoryInfoRoot { get; } = DataDirectory;
}
