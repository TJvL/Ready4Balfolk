namespace Ready4Balfolk.Domain.Helpers;

/// <summary>Where a path lies below another one, when it lies below it at all.</summary>
/// <remarks>
/// Four callers spelled this rule out separately and had begun to disagree about what counts as
/// below: a log line, a review row's folder, the folders a discovery preview shows, and the key a
/// folder's votes are gathered under. They answer the same question and now ask it in one place.
/// </remarks>
public static class RelativePath
{
    /// <summary>
    /// The part of <paramref name="path" /> under <paramref name="root" />, or nothing when it is
    /// not under it.
    /// </summary>
    /// <remarks>
    /// Nothing fails for a path outside the root: the walk up is spelled with "..", and on Windows
    /// a path on another volume comes back absolute. Both say where the path is rather than where
    /// it sits under the root. The root itself comes back as ".", which says nothing at all.
    /// </remarks>
    public static string? Below(string? root, string? path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrEmpty(path))
        {
            return null;
        }

        var relative = Path.GetRelativePath(root, path);

        return relative == "."
               || relative.StartsWith("..", StringComparison.Ordinal)
               || Path.IsPathRooted(relative)
            ? null
            : relative;
    }
}
