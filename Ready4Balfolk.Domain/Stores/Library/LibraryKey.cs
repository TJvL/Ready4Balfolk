namespace Ready4Balfolk.Domain.Stores.Library;

/// <summary>Turns a content hash into something a dictionary can key on.</summary>
/// <remarks>
/// Two equal hashes are two different arrays, and a dictionary keyed on <c>byte[]</c> would treat
/// them as two different tracks. Every lookup that crosses the boundary between the database and
/// memory goes through here rather than each caller inventing its own encoding.
/// </remarks>
public static class LibraryKey
{
    public static string For(byte[] contentHash) => Convert.ToHexStringLower(contentHash);
}
