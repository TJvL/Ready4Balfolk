using System.Buffers.Binary;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Identifies the audio in a file, ignoring its tags.</summary>
/// <remarks>
/// <para>
/// This is what makes the index survive a retag. Nothing in the application writes tags, but a
/// tagger the user runs on their library rewrites them, which would change a whole-file hash and make
/// the track look like a new one; hashing only the audio means the row stays put and keeps everything
/// the user decided about it.
/// </para>
/// <para>
/// It samples rather than reading everything. A library is tens of gigabytes and the first index has
/// to read every file once: on a fast desktop that was three minutes, but on a laptop with the music
/// on an external drive it is half an hour of watching a progress bar. Sampling turns that into
/// seconds, and the identity is just as good: two different recordings would have to share their
/// first slice, their last slice and their exact byte length to collide.
/// </para>
/// <para>
/// Where the audio is comes from TagLib for most formats, but not for FLAC and AIFF, which this
/// finds itself. TagLib reports a FLAC's range as the whole file, metadata blocks included, and a
/// tagged AIFF's range as its ID3 chunk alone: a fixed title then moved an approved FLAC to a new
/// row and back into review, and two AIFF recordings with the same tags became one track.
/// </para>
/// </remarks>
public static class AudioContentHasher
{
    /// <summary>How much is read from each end of the audio.</summary>
    private const int SampleSize = 256 * 1024;

    /// <summary>Below this, the whole thing is cheaper to read than to seek around in.</summary>
    private const int ReadEverythingBelow = SampleSize * 3;

    /// <summary>
    /// Hashes the audio of a file TagLib has read, given the range TagLib reported for it. For FLAC
    /// and AIFF that range is not the audio, so the file's own structure says where it is instead.
    /// </summary>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static byte[] Compute(IFileInfo fileInfo, AudioFormat format, long tagLibStart, long tagLibEnd)
    {
        using var stream = Open(fileInfo);

        var (start, end) = format switch
        {
            AudioFormat.Flac => FlacFrames(stream, tagLibStart, tagLibEnd) ?? (tagLibStart, tagLibEnd),
            // TagLib's range for a tagged AIFF is the one part never to hash, so a file that will
            // not walk is hashed whole rather than by it.
            AudioFormat.Aif => AiffSoundData(stream) ?? (0, stream.Length),
            _ => (tagLibStart, tagLibEnd)
        };

        return Hash(stream, start, end);
    }

    /// <summary>
    /// Hashes the audio between the tags, trusting the range it is given, so a leading ID3 block or
    /// a trailing tag is skipped rather than hashed.
    /// </summary>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static byte[] Compute(IFileInfo fileInfo, long audioStart, long audioEnd)
    {
        using var stream = Open(fileInfo);
        return Hash(stream, audioStart, audioEnd);
    }

    private static FileSystemStream Open(IFileInfo fileInfo) =>
        fileInfo.FileSystem.FileStream.New(fileInfo.FullName, FileMode.Open, FileAccess.Read,
            FileShare.Read, SampleSize, FileOptions.SequentialScan);

    /// <summary>The FLAC frames: everything after the last metadata block.</summary>
    /// <returns>Where the frames are, or null when the file does not walk as a FLAC.</returns>
    /// <remarks>
    /// The Vorbis comment and the pictures are metadata blocks, so a tagger rewrites the blocks and
    /// never the frames. TagLib's start is still taken, as the place the stream begins: it is past
    /// an ID3v2 tag some taggers put in front of the stream. So is its end, which is short of an
    /// ID3v1 or APE tag at the back; the frames otherwise run to the end of the file.
    /// </remarks>
    private static (long Start, long End)? FlacFrames(Stream stream, long tagLibStart, long tagLibEnd)
    {
        Span<byte> header = stackalloc byte[4];
        var position = Math.Max(tagLibStart, 0);

        if (!ReadAt(stream, position, header) || !header.SequenceEqual("fLaC"u8))
        {
            return null;
        }

        position += header.Length;
        while (true)
        {
            // A flag byte, its top bit marking the last block, then the block's length in 24 bits.
            if (!ReadAt(stream, position, header))
            {
                return null;
            }

            position += header.Length + ((header[1] << 16) | (header[2] << 8) | header[3]);

            if ((header[0] & 0x80) != 0)
            {
                return (position, tagLibEnd);
            }
        }
    }

    /// <summary>The sample data in an AIFF or AIFF-C file's SSND chunk.</summary>
    /// <returns>Where the samples are, or null when the file has no SSND chunk to find.</returns>
    /// <remarks>
    /// The ID3 chunk can come before the SSND chunk or after it, depending on the tagger, so the
    /// chunks are walked rather than either end trusted. IFF is big-endian, and a chunk with an odd
    /// length is followed by a pad byte its length does not count.
    /// </remarks>
    private static (long Start, long End)? AiffSoundData(Stream stream)
    {
        Span<byte> header = stackalloc byte[12];

        if (!ReadAt(stream, 0, header)
            || !header[..4].SequenceEqual("FORM"u8)
            || !(header[8..].SequenceEqual("AIFF"u8) || header[8..].SequenceEqual("AIFC"u8)))
        {
            return null;
        }

        var chunk = header[..8];
        long position = header.Length;
        while (true)
        {
            if (!ReadAt(stream, position, chunk))
            {
                return null;
            }

            var data = position + chunk.Length;
            long size = BinaryPrimitives.ReadUInt32BigEndian(chunk[4..]);

            if (chunk[..4].SequenceEqual("SSND"u8))
            {
                // The samples start after an offset and a block size, plus however many bytes the
                // offset says to skip for alignment.
                return ReadAt(stream, data, chunk)
                    ? (data + chunk.Length + BinaryPrimitives.ReadUInt32BigEndian(chunk[..4]), data + size)
                    : null;
            }

            position = data + size + (size & 1);
        }
    }

    /// <returns>False when the file is too short to hold what is asked for.</returns>
    private static bool ReadAt(Stream stream, long position, Span<byte> buffer)
    {
        if (position < 0 || position > stream.Length - buffer.Length)
        {
            return false;
        }

        stream.Seek(position, SeekOrigin.Begin);
        stream.ReadExactly(buffer);
        return true;
    }

    private static byte[] Hash(Stream stream, long audioStart, long audioEnd)
    {
        var start = Math.Clamp(audioStart, 0, stream.Length);
        // A negative or unknown end position means nobody could say, so use the end of the file
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
