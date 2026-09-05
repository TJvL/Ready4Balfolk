namespace Ready4Balfolk.Domain.Services.Library;

/// <summary>A folder the index holds tracks in that this scan found no music in.</summary>
/// <param name="Path">The folder itself, as the filesystem spells it.</param>
/// <param name="TrackCount">How many indexed paths sit in it or under it.</param>
/// <param name="Error">
/// Why it would not open, in the words the filesystem used, or null when it opened and simply held
/// no music. The two are different things to read and the difference is all anybody has to go on.
/// </param>
public sealed record MissingLibraryFolder(string Path, int TrackCount, string? Error = null);

/// <summary>What a person answered about folders a scan found no music in.</summary>
/// <remarks>
/// There is no fourth option and no "do not ask again". A scan cannot tell a drive that has not
/// mounted from a folder emptied on purpose, so it asks every time it cannot tell.
/// </remarks>
public enum MissingFolderAnswer
{
    /// <summary>
    /// Keep the tracks, marked unavailable.
    /// </summary>
    /// <remarks>
    /// The answer for a dead NAS ten minutes before a gig: whatever is reachable still works, and
    /// nothing is lost. They come back by themselves when a later scan finds their files.
    /// </remarks>
    KeepThem,

    /// <summary>The library really is empty there: reconcile them away, approvals and all.</summary>
    ForgetThem,

    /// <summary>Write nothing. Mount the drive or fix the permissions, then start again.</summary>
    Exit
}

/// <summary>
/// Which folders a scan cannot speak for, worked out from the walk and the index alone.
/// </summary>
/// <remarks>
/// Pure, so it is tested without a filesystem under it. Everything it decides comes from two
/// things: the folders the walk found music in, and the paths the index already holds.
/// </remarks>
public static class MissingFolders
{
    /// <summary>The folders the index holds tracks in that this scan found no music in.</summary>
    /// <param name="indexedPaths">Every path the index holds.</param>
    /// <param name="directoriesWithMusic">The folders the walk opened and found audio files in.</param>
    /// <param name="unreadableDirectories">The folders the walk could not open, and why.</param>
    /// <param name="root">The music directory, which bounds all of this.</param>
    /// <remarks>
    /// <para>
    /// Reported as high up as the evidence allows. A folder that found music speaks for every
    /// folder above it, because the walk had to open all of them to get there, so a subtree with no
    /// music anywhere in it is reported as its own topmost folder rather than as the four hundred
    /// folders underneath. That is also what makes the count on the dialog the number a person
    /// recognises: the whole of what is gone, not a slice of it.
    /// </para>
    /// <para>
    /// A folder that opened, held no music of its own, and has a folder under it that did is
    /// therefore not reported. It cannot be a drive that failed to mount: a failed mount is empty
    /// all the way down, and a folder that would not open takes everything below it with it. What
    /// is left is files somebody deleted, which is ordinary housekeeping and reconciles silently.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MissingLibraryFolder> Detect(
        IEnumerable<string> indexedPaths,
        IReadOnlySet<string> directoriesWithMusic,
        IReadOnlyDictionary<string, string> unreadableDirectories,
        string root)
    {
        ArgumentNullException.ThrowIfNull(indexedPaths);
        ArgumentNullException.ThrowIfNull(directoriesWithMusic);
        ArgumentNullException.ThrowIfNull(unreadableDirectories);

        var rootKey = Normalise(root ?? string.Empty);
        if (rootKey.Length == 0)
        {
            return [];
        }

        // Every folder that holds indexed tracks and every folder between it and the root, so a
        // report has somewhere higher up to land.
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in indexedPaths)
        {
            if (FolderOf(path) is not { } folder || !IsAtOrUnder(folder, rootKey))
            {
                continue;
            }

            foreach (var node in FolderAndAncestors(folder, rootKey))
            {
                counts[node] = counts.GetValueOrDefault(node) + 1;
            }
        }

        if (counts.Count == 0)
        {
            // Nothing is at risk, so there is nothing to ask about. A genuine first run lands here.
            return [];
        }

        var spokenFor = FoldersTheWalkOpened(directoriesWithMusic, rootKey);
        var errors = unreadableDirectories.ToDictionary(
            pair => Normalise(pair.Key), pair => pair.Value, StringComparer.Ordinal);

        return
        [
            .. counts.Keys
                .Where(folder => IsTopOfAMissingSubtree(folder, rootKey, spokenFor))
                .Select(folder => new MissingLibraryFolder(
                    folder, counts[folder], errors.GetValueOrDefault(folder)))
                .OrderBy(folder => folder.Path, StringComparer.Ordinal)
        ];
    }

    /// <summary>The indexed paths that sit in the folders reported as missing.</summary>
    public static IReadOnlyCollection<string> PathsIn(
        IEnumerable<string> indexedPaths, IReadOnlyCollection<MissingLibraryFolder> folders)
    {
        ArgumentNullException.ThrowIfNull(indexedPaths);
        ArgumentNullException.ThrowIfNull(folders);

        return
        [
            .. indexedPaths.Where(path =>
                FolderOf(path) is { } folder && folders.Any(missing => IsAtOrUnder(folder, missing.Path)))
        ];
    }

    /// <summary>Every folder the walk can vouch for, which is each one it found music in and all above it.</summary>
    private static HashSet<string> FoldersTheWalkOpened(IReadOnlySet<string> directoriesWithMusic, string rootKey)
    {
        var opened = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in directoriesWithMusic)
        {
            var found = Normalise(directory);
            if (!IsAtOrUnder(found, rootKey))
            {
                continue;
            }

            foreach (var node in FolderAndAncestors(found, rootKey))
            {
                if (!opened.Add(node))
                {
                    // Another folder has already marked the rest of the chain.
                    break;
                }
            }
        }

        return opened;
    }

    /// <summary>A folder and every folder between it and the music directory, closest first.</summary>
    private static IEnumerable<string> FolderAndAncestors(string folder, string rootKey)
    {
        var node = folder;
        while (true)
        {
            yield return node;

            if (ParentOf(node, rootKey) is not { } parent)
            {
                yield break;
            }

            node = parent;
        }
    }

    /// <summary>Whether this is the highest folder of a run that found no music at all.</summary>
    private static bool IsTopOfAMissingSubtree(string folder, string rootKey, HashSet<string> spokenFor) =>
        !spokenFor.Contains(folder)
        && (string.Equals(folder, rootKey, StringComparison.Ordinal)
            || (ParentOf(folder, rootKey) is { } parent && spokenFor.Contains(parent)));

    private static string? ParentOf(string node, string rootKey) =>
        string.Equals(node, rootKey, StringComparison.Ordinal)
            ? null
            : FolderOf(node) is { } parent && IsAtOrUnder(parent, rootKey)
                ? parent
                : null;

    private static string? FolderOf(string path) =>
        Path.GetDirectoryName(path) is { Length: > 0 } folder ? Normalise(folder) : null;

    private static bool IsAtOrUnder(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>A folder path in the one shape both sides of a comparison use.</summary>
    private static string Normalise(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return trimmed.Length == 0 ? path : trimmed;
    }
}
