using System.IO.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.History;

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

        // A machine that has been used before has a dance list on it, because the application
        // ships none: it arrived by being fetched or imported, and it is a file like any other.
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "dances.json"),
            Path.Combine(DirectoryInfoRoot.FullName, "dance_list.json"));

        // A window with a size, because that is what a machine that has been used before holds, and
        // because a list only builds the rows that fit in it: a window of nothing shows nothing.
        _settings = _settings with
        {
            MusicDirectoryPath = MusicDirectory.FullName,
            SetupCompleted = true,
            MainWindowState = new WindowState(0, 0, 1600, 1000)
        };
    }

    /// <summary>Where the application keeps what it writes.</summary>
    public IDirectoryInfo DirectoryInfoRoot { get; }

    /// <summary>The directory the DJ keeps their music in.</summary>
    public IDirectoryInfo MusicDirectory { get; }

    /// <summary>An empty machine: a music directory, a data directory, and nothing in either.</summary>
    public static ScenarioWorld Create()
    {
        SweepUpAfterRunsThatDidNotFinish();

        return new ScenarioWorld(Path.Combine(Path.GetTempPath(), $"r4b_e2e_{Guid.NewGuid():N}"));
    }

    /// <summary>Throws away the worlds of runs that were killed before they could tidy up.</summary>
    /// <remarks>
    /// A scenario deletes its own world, but a run that is interrupted, or one whose application
    /// still had a file open, leaves one behind. An hour is long enough that nothing belonging to a
    /// run in progress is ever touched.
    /// </remarks>
    private static void SweepUpAfterRunsThatDidNotFinish()
    {
        var abandoned = Directory
            .EnumerateDirectories(Path.GetTempPath(), "r4b_e2e_*")
            .Where(directory => Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddHours(-1));

        foreach (var directory in abandoned)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

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

    /// <summary>A dances.json on this machine, the way one arrives on a stick.</summary>
    public string ADanceListFileHolding(string contents)
    {
        var path = Path.Combine(_root, $"dances-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, contents);

        return path;
    }

    /// <summary>The published list, as a file rather than as this machine's own copy.</summary>
    public string ThePublishedDanceListAsAFile()
    {
        var path = Path.Combine(_root, "dances-from-a-stick.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dances.json"), path, overwrite: true);

        return path;
    }

    /// <summary>An evening that ran and was never closed, this long ago.</summary>
    /// <remarks>
    /// Written by the real history store into this world's own file, the way the settings are
    /// written by hand: the machine simply has a night on it that nobody ended, which is what a
    /// flat battery or a laptop lid leaves behind.
    /// </remarks>
    public ScenarioWorld WithAnEveningNobodyEnded(TimeSpan howLongAgo)
    {
        using var history = new QueueHistoryStore(
            this, _fileSystem, new NoOpLoggerService(), new AnHourInThePast(howLongAgo));

        var stopped = DateTime.Now - howLongAgo;

        history.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        history.AddAsync(new TrackHistoryEntry(
            Path.Combine(MusicDirectory.FullName, "the last dance.mp3"),
            "Mazurka",
            "Naragonia",
            "Salamandre",
            TimeSpan.FromMinutes(3),
            RandomlyAdded: false,
            CompletionStatus.Finished,
            stopped - TimeSpan.FromMinutes(3),
            stopped)).GetAwaiter().GetResult();

        LastDanceEndedAt = stopped;

        return this;
    }

    /// <summary>When the evening nobody ended actually stopped.</summary>
    public DateTime LastDanceEndedAt { get; private set; }

    /// <summary>A clock that says the evening happened a while ago.</summary>
    private sealed class AnHourInThePast(TimeSpan howLongAgo) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => System.GetUtcNow() - howLongAgo;
    }

    /// <summary>A machine nobody has fetched or imported a dance list on.</summary>
    public ScenarioWorld WhereThereIsNoDanceList()
    {
        File.Delete(Path.Combine(DirectoryInfoRoot.FullName, "dance_list.json"));
        return this;
    }

    /// <summary>A file in the music directory that is not audio, whatever its name says.</summary>
    public ScenarioWorld WithUnreadableTrack(string fileName)
    {
        File.WriteAllBytes(
            Path.Combine(MusicDirectory.FullName, fileName),
            [.. Enumerable.Range(0, 4096).Select(index => (byte)(index % 251))]);

        return this;
    }

    /// <summary>Renames a track behind the application's back, the way a tidy-up does.</summary>
    public void RenameTrackFile(string titleInFileName, string newFileName)
    {
        var file = Directory.EnumerateFiles(MusicDirectory.FullName)
            .First(path => path.Contains(titleInFileName, StringComparison.OrdinalIgnoreCase));

        File.Move(file, Path.Combine(MusicDirectory.FullName, newFileName));
    }

    /// <summary>Takes a track away behind the application's back, the way a tidy-up does.</summary>
    public void RemoveTrackFile(string titleInFileName)
    {
        var file = Directory.EnumerateFiles(MusicDirectory.FullName)
            .First(path => path.Contains(titleInFileName, StringComparison.OrdinalIgnoreCase));

        File.Delete(file);
    }

    /// <summary>Serves the display, and the remote when a PIN is given, on a port nobody is using.</summary>
    /// <remarks>
    /// The port is taken from the operating system rather than picked, because scenarios run beside
    /// each other and two servers on one port is one scenario failing for the other's reasons.
    /// </remarks>
    public ScenarioWorld WithTheServerOn(string remotePin = "")
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        WebServerPort = ((IPEndPoint)probe.LocalEndPoint!).Port;

        return WithSettings(settings => settings with
        {
            WebServerEnabled = true,
            WebServerPort = WebServerPort,
            WebRemoteControlEnabled = remotePin.Length > 0,
            WebRemoteControlPin = remotePin
        });
    }

    /// <summary>Somebody else is already listening on the port the DJ chose.</summary>
    /// <remarks>
    /// Returned so the scenario can hold it: the port is only taken for as long as this is alive,
    /// which is what makes the state the application reports the state of a real conflict.
    /// </remarks>
    public Socket WhereSomethingElseHasThePort()
    {
        var squatter = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        squatter.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        squatter.Listen();

        WebServerPort = ((IPEndPoint)squatter.LocalEndPoint!).Port;
        WithSettings(settings => settings with { WebServerEnabled = true, WebServerPort = WebServerPort });

        return squatter;
    }

    /// <summary>Where a browser reaches this world's application.</summary>
    public string ServerAddress => $"http://127.0.0.1:{WebServerPort}";

    /// <summary>The port the server was given, or zero when there is no server.</summary>
    public int WebServerPort { get; private set; }

    /// <summary>The file this DJ has nominated as the sound of the evening ending.</summary>
    /// <remarks>
    /// Outside the music directory on purpose: it is one file the user points at, not part of the
    /// library, and it is never queued as a dance.
    /// </remarks>
    public ScenarioWorld WithEndOfNightAudio()
    {
        var path = Path.Combine(_root, "the end of the night.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Media", "scale.ogg"), path, overwrite: true);

        return WithSettings(settings => settings with { EndOfNightAudioPath = path });
    }

    /// <summary>Takes the closing track away, which nothing is watching for.</summary>
    public void RemoveTheEndOfNightFile() =>
        File.Delete(Path.Combine(_root, "the end of the night.mp3"));

    /// <summary>The file was nominated, and is not there any more.</summary>
    public ScenarioWorld WhereTheEndOfNightFileHasBeenMovedAway() =>
        WithSettings(settings => settings with
        {
            EndOfNightAudioPath = Path.Combine(_root, "the end of the night.mp3")
        });

    /// <summary>A machine nobody has set up: the wizard is what opens.</summary>
    /// <remarks>
    /// The music directory goes with it. Setup is where a person says where their music is, so a
    /// world that has not been set up cannot already know.
    /// </remarks>
    public ScenarioWorld WhereNothingHasBeenSetUpYet() =>
        WithSettings(settings => settings with
        {
            SetupCompleted = false,
            MusicDirectoryPath = string.Empty
        });

    /// <summary>The DJ has declared which tag field holds what, so their library needs no review.</summary>
    public ScenarioWorld WhereTheTagsAreTrusted() =>
        WithSettings(settings => settings with
        {
            DiscoveryOrNull = new DiscoverySettings
            {
                UsesTagTrust = true,
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

    /// <summary>The queue stops accepting entries as of now, so the next dance is already too late.</summary>
    public ScenarioWorld WhereTheCutoffHasArrived(int graceMinutes = 0) =>
        WithSettings(settings => settings with
        {
            QueueCutoffEnabled = true,
            QueueCutoffMinutesOfDay = (int)DateTime.Now.TimeOfDay.TotalMinutes,
            QueueCutoffGraceMinutes = graceMinutes
        });

    /// <summary>A settings file that is not settings, which is what an interrupted write leaves.</summary>
    public ScenarioWorld WhereTheSettingsFileIsCorrupt()
    {
        Save();
        File.WriteAllText(Path.Combine(DirectoryInfoRoot.FullName, "settings.json"), "{ this is not settings");

        return this;
    }

    /// <summary>Whether this world has any way to reach the internet. It has not, unless it says.</summary>
    /// <remarks>
    /// A hall with no wifi is where most of these evenings happen, and it is also the honest
    /// default for a scenario: nothing here asserts anything that comes off the network, and a run
    /// that asks BigBalfolkList for the dance list on every scenario is a run that fails when
    /// GitHub does. Nothing is substituted for it: the scenario's process is pointed at a proxy
    /// that is not listening, so every request fails the way it fails in a cellar.
    /// </remarks>
    public bool HasInternet { get; private set; }

    /// <summary>This world can reach the internet, for the scenarios that are about reaching it.</summary>
    public ScenarioWorld WhereThereIsInternet()
    {
        HasInternet = true;
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
