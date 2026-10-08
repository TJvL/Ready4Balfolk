using System.IO.Abstractions;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Stores.Tracks;

/// <summary>What a batch of watcher reports asks of the index, apart from reading files.</summary>
/// <remarks>
/// Worked out against the paths the index held when the batch closed, because a folder event names
/// only the folder and the index is what knows which tracks were under it. Nothing here touches the
/// disk or the index, so the rules about deletes and moves meeting in one window can be stated on
/// their own rather than by raising watcher events at a whole store.
/// </remarks>
/// <param name="Vanished">Every path that is gone, the files under a folder that went included.</param>
/// <param name="Forget">
/// Every row to delete: what vanished, and every path a file read again takes the place of.
/// </param>
/// <param name="Repoint">Rows the index moves to a new path rather than reading them again.</param>
/// <param name="Moved">
/// Every path move anything else holding a path has to be told about, in the order they happened to
/// be worked out: the folders first, then the files renamed on their own.
/// </param>
public sealed record WatchedBatchPlan(
    IReadOnlyList<string> Vanished,
    IReadOnlyList<string> Forget,
    IReadOnlyList<PathMove> Repoint,
    IReadOnlyList<PathMove> Moved)
{
    /// <summary>Works out what a batch means for the rows the index holds.</summary>
    /// <param name="batch">What the watcher's reports added up to.</param>
    /// <param name="indexed">Every path the index held when the batch closed.</param>
    /// <param name="path">What tells a folder's separator apart, so a sibling is not taken for a child.</param>
    public static WatchedBatchPlan For(WatchedBatch batch, IEnumerable<string> indexed, IPath path)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(indexed);
        ArgumentNullException.ThrowIfNull(path);

        var vanished = new List<string>(batch.Gone);
        foreach (var folder in batch.GoneFolders)
        {
            vanished.AddRange(PathsUnder(indexed, folder, path));
        }

        // The index as well as the list, or the next rebuild resurrects what is gone. Even where
        // nothing published matched: a file still sitting in review has an index row too.
        //
        // Gathered before anything is re-pointed, because these are the paths the events named and
        // a row that is about to be forgotten must not be renamed out from under the delete by a
        // folder that moved in the same window.
        var forget = new List<string>(vanished);
        foreach (var (_, replaces) in batch.ToRead)
        {
            if (replaces is not null)
            {
                forget.Add(replaces);
            }
        }

        var forgotten = forget.ToHashSet(StringComparer.Ordinal);

        // A folder that moved still holds the same audio, so its rows move with it rather than
        // being read again. The hash is what every approval hangs on and it cannot have changed,
        // and opening a folder's worth of files to arrive back at hashes already in hand is not
        // what to be doing halfway through an evening.
        //
        // Moved rather than written and deleted: a write says the file was just read off the disk
        // and marks the row reachable, so a row being kept as unreachable would come back into the
        // library at a path nobody has ever seen a file at.
        var repoint = new List<PathMove>();
        foreach (var (from, to) in batch.Moved)
        {
            foreach (var under in PathsUnder(indexed, from, path))
            {
                if (forgotten.Contains(under))
                {
                    // Thrown away in the same window as the folder around it was tidied up. Moving
                    // its row would rename it past the delete that is coming for it, and a file the
                    // DJ deleted would be back in the library, in the pool and in the random pick
                    // under the folder's new name, pointing at nothing.
                    continue;
                }

                repoint.Add(new PathMove(under, string.Concat(to, under.AsSpan(from.Length))));
            }
        }

        // What the index has to re-point, and what everything else holding a path has to be told
        // about. A file renamed on its own is in the second only: it is read again, because it may
        // well have been retagged as it was, and the row it writes is the answer to where it went.
        var moved = new List<PathMove>(repoint);
        foreach (var (read, replaces) in batch.ToRead)
        {
            if (replaces is not null && SupportedAudioFormats.IsSupported(replaces))
            {
                moved.Add(new PathMove(replaces, read));
            }
        }

        return new WatchedBatchPlan(vanished, forget, repoint, moved);
    }

    /// <summary>Every known path under a folder, which is what one event about that folder covers.</summary>
    private static IEnumerable<string> PathsUnder(IEnumerable<string> paths, string folder, IPath path) =>
        paths.Where(candidate =>
            candidate.Length > folder.Length
            && candidate.StartsWith(folder, StringComparison.Ordinal)
            && (candidate[folder.Length] == path.DirectorySeparatorChar
                || candidate[folder.Length] == path.AltDirectorySeparatorChar));
}
