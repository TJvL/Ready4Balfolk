using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Stores;

namespace Ready4Balfolk.E2E;

/// <summary>The world a scenario starts in: files on disk, and nothing running yet.</summary>
/// <remarks>
/// <para>
/// Setup is the world, not the application. Everything here is what a DJ's machine would already
/// hold before they double click anything: a music directory, a settings file, whatever they have
/// declared about their library. The application is then started and left to find it.
/// </para>
/// <para>
/// The directory is the scenario's own, which is what makes the stores real rather than
/// substituted: settings, history, the library index and the dance list are the shipped
/// implementations writing real files that no other scenario can see.
/// </para>
/// </remarks>
public sealed class ScenarioWorld : IApplicationSettingsDirectory, IDisposable
{
    private static readonly JsonSerializerOptions SettingsJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FileSystem _fileSystem = new();
    private readonly string _root;

    private ApplicationSettings _settings = new();

    private ScenarioWorld(string root)
    {
        _root = root;
        DirectoryInfoRoot = _fileSystem.DirectoryInfo.New(Path.Combine(root, "data"));
        MusicDirectory = _fileSystem.DirectoryInfo.New(Path.Combine(root, "music"));
        DirectoryInfoRoot.Create();
        MusicDirectory.Create();

        _settings = _settings with { MusicDirectoryPath = MusicDirectory.FullName, SetupCompleted = true };
    }

    /// <summary>Where the application keeps what it writes.</summary>
    public IDirectoryInfo DirectoryInfoRoot { get; }

    /// <summary>The directory the DJ keeps their music in.</summary>
    public IDirectoryInfo MusicDirectory { get; }

    /// <summary>An empty machine: a music directory, a data directory, and nothing in either.</summary>
    public static ScenarioWorld Create() =>
        new(Path.Combine(Path.GetTempPath(), $"r4b_e2e_{Guid.NewGuid():N}"));

    /// <summary>A track in the music directory, tagged the way this world's DJ tags theirs.</summary>
    /// <remarks>
    /// The dance goes in the comment, which is only meaningful alongside
    /// <see cref="WhereTheTagsAreTrusted"/>: a tag field speaks for a track field because the user
    /// said it does, never because the application assumed.
    /// </remarks>
    public ScenarioWorld WithTrack(string dance, string artist, string title, string fileName = "")
    {
        var name = string.IsNullOrEmpty(fileName)
            ? $"{artist} - {title}.mp3"
            : fileName;
        var path = Path.Combine(MusicDirectory.FullName, name);

        File.Copy(Path.Combine(AppContext.BaseDirectory, "Media", "scale.mp3"), path, overwrite: true);

        // Every track gets its own tail of bytes, because the application keys a recording by the
        // hash of its audio rather than by its path: two copies of one file are one track to it,
        // correctly, and a world made of copies would hand a scenario a library of one.
        File.AppendAllText(path, $"\u0000{Guid.NewGuid():N}");

        using var file = TagLib.File.Create(path);
        file.Tag.Title = title;
        file.Tag.Performers = [artist];
        file.Tag.AlbumArtists = [artist];
        file.Tag.Comment = dance;
        file.Save();

        return this;
    }

    /// <summary>The DJ has declared which tag field holds what, so their library needs no review.</summary>
    public ScenarioWorld WhereTheTagsAreTrusted() =>
        WithSettings(settings => settings with
        {
            DiscoveryOrNull = new DiscoverySettings
            {
                TagTrust = new TagTrust
                {
                    Artist = [TagField.Artist],
                    Title = [TagField.Title],
                    Dance = [TagField.Comment]
                }
            }
        });

    /// <summary>Anything else this world's settings file says.</summary>
    public ScenarioWorld WithSettings(Func<ApplicationSettings, ApplicationSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        _settings = change(_settings);
        return this;
    }

    /// <summary>Writes the settings file, which is the last thing to happen before the app starts.</summary>
    public ScenarioWorld Save()
    {
        File.WriteAllText(
            Path.Combine(DirectoryInfoRoot.FullName, "settings.json"),
            JsonSerializer.Serialize(_settings, SettingsJson));

        return this;
    }

    /// <summary>Reads the settings file back, to assert on what the application saved.</summary>
    public ApplicationSettings SettingsOnDisk()
    {
        var path = Path.Combine(DirectoryInfoRoot.FullName, "settings.json");
        return JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(path), SettingsJson)
               ?? new ApplicationSettings();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A scenario that left a file handle open is a scenario problem, not a reason to fail
            // the run in the cleanup: the temp directory is named per run and goes with the disk.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
