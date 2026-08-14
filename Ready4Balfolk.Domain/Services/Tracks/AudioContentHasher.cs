using System.Buffers.Binary;
using System.IO.Abstractions;
using System.Security.Cryptography;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Identifies the audio in a file, ignoring its tags.</summary>
/// <remarks>
/// <para>
/// This is what makes the index survive the application's own edits. Writing a corrected dance name
/// into a file rewrites its tags, which would change a whole-file hash and make the track look like
/// a new one; hashing only the audio means the row stays put and keeps everything the user decided
/// about it.
/// </para>
/// <para>
/// It samples rather than reading everything. A library is tens of gigabytes and the first index has
/// to read every file once: on a fast desktop that was three minutes, but on a laptop with the music
/// on an external drive it is half an hour of watching a progress bar. Sampling turns that into
/// seconds, and the identity is just as good — two different recordings would have to share their
/// first slice, their last slice and their exact byte length to collide.
/// </para>
/// </remarks>
public static class AudioContentHasher
{
    /// <summary>How much is read from each end of the audio.</summary>
    private const int SampleSize = 256 * 1024;

    /// <summary>Below this, the whole thing is cheaper to read than to seek around in.</summary>
    private const int ReadEverythingBelow = SampleSize * 3;

    /// <summary>
    /// Hashes the audio between the tags. TagLib reports where the audio starts and ends, so a
    /// leading ID3 block or a trailing tag is skipped rather than hashed.
    /// </summary>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static byte[] Compute(IFileInfo fileInfo, long audioStart, long audioEnd)
    {
        using var stream = fileInfo.FileSystem.FileStream.New(fileInfo.FullName, FileMode.Open, FileAccess.Read,
            FileShare.Read, SampleSize, FileOptions.SequentialScan);

        var start = Math.Clamp(audioStart, 0, stream.Length);
        // A negative or unknown end position means TagLib could not say, so use the end of the file
        // rather than hashing nothing.
        var end = audioEnd <= start ? stream.Length : Math.Min(audioEnd, stream.Length);
        var length = end - start;

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // The length goes in first, so two files sharing both sampled slices still differ unless
        // they are the same size to the byte.
        Span<byte> lengthBytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, length);
        hasher.AppendData(lengthBytes);

        if (length <= ReadEverythingBelow)
        {
            AppendRange(stream, hasher, start, length);
        }
        else
        {
            AppendRange(stream, hasher, start, SampleSize);
            AppendRange(stream, hasher, end - SampleSize, SampleSize);
        }

        return hasher.GetHashAndReset();
    }

    private static void AppendRange(Stream stream, IncrementalHash hasher, long from, long count)
    {
        stream.Seek(from, SeekOrigin.Begin);

        var buffer = new byte[SampleSize];
        var remaining = count;
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0)
            {
                return;
            }

            hasher.AppendData(buffer, 0, read);
            remaining -= read;
        }
    }
}
