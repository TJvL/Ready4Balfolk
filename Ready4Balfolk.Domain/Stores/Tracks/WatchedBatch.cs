namespace Ready4Balfolk.Domain.Stores.Tracks;

/// <summary>What one batch of watcher reports adds up to.</summary>
/// <remarks>
/// <para>
/// Worked out on the watcher's thread and acted on once, so that copying an album in is one index
/// write and one rebuild rather than one of each per file.
/// </para>
/// <para>
/// The watcher reports everything under the music directory; which of it matters is the library's
/// business, not the watcher's, and this is where that is decided. It was part of TrackStore and
/// reachable only by raising watcher events at a whole store.
/// </para>
/// </remarks>
public sealed class WatchedBatch
{
    private readonly List<(string Path, string? Replaces)> _toRead = [];
    private readonly List<string> _gone = [];
    private readonly List<string> _goneFolders = [];
    private readonly List<(string From, string To)> _moved = [];

    private WatchedBatch()
    {
    }

    /// <summary>Files to read, each with the path it takes the place of, if it takes one.</summary>
    public IReadOnlyList<(string Path, string? Replaces)> ToRead => _toRead;

    /// <summary>Files that are gone, whose queued entries go with them.</summary>
    public IReadOnlyList<string> Gone => _gone;

    /// <summary>Folders that are gone, standing for every path underneath them.</summary>
    public IReadOnlyList<string> GoneFolders => _goneFolders;

    /// <summary>Folders that moved, from their old name to their new one.</summary>
    public IReadOnlyList<(string From, string To)> Moved => _moved;

    /// <summary>Whether nothing the watcher reported is anything the library cares about.</summary>
    public bool IsEmpty =>
        _toRead.Count == 0 && _gone.Count == 0 && _goneFolders.Count == 0 && _moved.Count == 0;

    /// <summary>Decides what the changes the watcher noticed are worth.</summary>
    /// <param name="changes">What the watcher reported, in the order it reported it.</param>
    public static WatchedBatch From(IEnumerable<LibraryFileChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var batch = new WatchedBatch();

        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case LibraryFileChangeKind.Appeared:
                    if (SupportedAudioFormats.IsSupported(change.Path))
                    {
                        batch._toRead.Add((change.Path, null));
                    }

                    break;
                case LibraryFileChangeKind.Vanished:
                    // A folder is one event for everything under it: nothing inside is reported
                    // file by file, so tidying one up in a file manager used to leave every track
                    // it held in the library, the pool and the random pick, pointing nowhere.
                    if (SupportedAudioFormats.IsSupported(change.Path))
                    {
                        batch._gone.Add(change.Path);
                    }
                    else
                    {
                        batch._goneFolders.Add(change.Path);
                    }

                    break;
                case LibraryFileChangeKind.Renamed:
                    batch.AddRename(change.Path, change.PreviousPath!);
                    break;
                default:
                    break;
            }
        }

        return batch;
    }

    private void AddRename(string path, string previousPath)
    {
        if (SupportedAudioFormats.IsSupported(path))
        {
            // The audio is unchanged, so its content hash and everything approved about it are
            // too. A rename is a path changing, not a track appearing.
            _toRead.Add((path, previousPath));
            return;
        }

        if (SupportedAudioFormats.IsSupported(previousPath))
        {
            // Renamed out of the formats this reads: to the index that is the file going away.
            _gone.Add(previousPath);
            return;
        }

        // Neither end is audio, so this is a folder being renamed or moved within the music
        // directory. What is under it is never reported separately.
        _moved.Add((previousPath, path));
    }
}
