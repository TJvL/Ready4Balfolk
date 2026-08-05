namespace Ready4Balfolk.Domain.Stores;

public class ApplicationSettingsDirectory : IApplicationSettingsDirectory
{
    private static readonly DirectoryInfo DataDirectory =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ready4Balfolk"));

    public DirectoryInfo DirectoryInfoRoot { get; } = DataDirectory;
}
