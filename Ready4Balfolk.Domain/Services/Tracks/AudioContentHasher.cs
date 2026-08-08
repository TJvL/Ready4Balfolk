using System.Security.Cryptography;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Hashes the audio in a file, ignoring its tags.</summary>
/// <remarks>
/// This is what makes the index survive the application's own edits. Writing a corrected dance name
/// into a file rewrites its tags, which would change a whole-file hash and make the track look like
/// a new one; hashing only the audio means the row stays put and keeps everything the user decided
/// about it.
/// </remarks>
public static class AudioContentHasher
{
    private const int BufferSize = 1 << 20;

    /// <summary>
    /// Hashes the bytes between the tags. TagLib reports where the audio starts and ends, so a
    /// leading ID3 block or a trailing tag is skipped rather than hashed.
    /// </summary>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static byte[] Compute(FileInfo fileInfo, long audioStart, long audioEnd)
    {
        using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);

        var start = Math.Clamp(audioStart, 0, stream.Length);
        // A negative or unknown end position means TagLib could not say, so hash to the end of the
        // file rather than hashing nothing.
        var end = audioEnd <= start ? stream.Length : Math.Min(audioEnd, stream.Length);

        stream.Seek(start, SeekOrigin.Begin);

        using var hasher = SHA256.Create();
        var buffer = new byte[BufferSize];
        var remaining = end - start;

        while (remaining > 0)
        {
            var wanted = (int)Math.Min(buffer.Length, remaining);
            var read = stream.Read(buffer, 0, wanted);
            if (read <= 0)
            {
                break;
            }

            hasher.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        hasher.TransformFinalBlock([], 0, 0);
        return hasher.Hash ?? [];
    }
}
