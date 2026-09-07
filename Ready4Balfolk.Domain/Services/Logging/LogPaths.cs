using Ready4Balfolk.Domain.Helpers;

namespace Ready4Balfolk.Domain.Services.Logging;

/// <summary>How a path goes into the log, and what comes back out of one on the way to a stranger.</summary>
/// <remarks>
/// <para>
/// The log is exported by a button that asks for the file to be pasted into a public issue, so
/// every absolute path in it is somebody's home directory, the name that is usually in it, and the
/// shape of their disk, handed to whoever reads that issue. What the log is for is saying which
/// file would not open, and a name below the music root says that without saying where the music
/// root is.
/// </para>
/// <para>
/// <see cref="WithoutUserProfile" /> is the backstop for what no call site can reach: the text of
/// an exception is written by whoever threw it, and those messages routinely carry the path the
/// call failed on.
/// </para>
/// </remarks>
public static class LogPaths
{
    /// <summary>The file or folder's own name, with nothing above it.</summary>
    /// <remarks>
    /// A volume or a filesystem root is its own name: "E:\" and "/" have nothing above them to
    /// drop, and Path.GetFileName answers both with an empty string. A music directory pointed at a
    /// whole drive is exactly the case the missing-directory warning exists for, and a warning
    /// naming '' has lost the one fact it was written to carry.
    /// </remarks>
    public static string Name(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }

        var trimmed = Path.TrimEndingDirectorySeparator(path);
        var name = Path.GetFileName(trimmed);

        return name.Length == 0 ? trimmed : name;
    }

    /// <summary>
    /// Where <paramref name="path" /> lies below <paramref name="root" />, or its name alone when
    /// it lies somewhere else.
    /// </summary>
    public static string Below(string? root, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }

        // A path the root cannot account for has nothing left that is safe to write but its own
        // name: what RelativePath refuses to answer with is a way from the root to the file, and
        // spelling that out is the thing being kept out of the log.
        return RelativePath.Below(root, path) ?? Name(path);
    }

    /// <summary>The text with the user profile directory written as a tilde.</summary>
    /// <remarks>
    /// Case-insensitively on Windows, where two spellings of the same directory are the same
    /// directory, and exactly on the other platforms, where they are not.
    /// </remarks>
    public static string WithoutUserProfile(string text, string? userProfile) =>
        string.IsNullOrEmpty(text) || string.IsNullOrEmpty(userProfile)
            ? text
            : text.Replace(
                Path.TrimEndingDirectorySeparator(userProfile),
                "~",
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
