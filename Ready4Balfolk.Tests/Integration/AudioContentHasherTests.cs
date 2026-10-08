using System.Buffers.Binary;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Tests.Integration;

public sealed class AudioContentHasherTests
{
    private readonly MockFileSystem _fileSystem = new();

    public AudioContentHasherTests()
    {
        _fileSystem.Directory.CreateDirectory("/hash");
    }

    [Fact]
    public void ChangingTheTagsDoesNotChangeTheHash()
    {
        // The layout of a tagged file: a header, the audio, a trailer. Only the middle is hashed,
        // which is what lets the application rewrite a dance name into a file without the index
        // deciding it is looking at a new track.
        var before = Write("before", "HEADER-v1"u8, "AUDIO-AUDIO-AUDIO"u8, "TRAILER-v1"u8);
        var after = Write("after", "HEADER-version-two"u8, "AUDIO-AUDIO-AUDIO"u8, "TRAILER-longer"u8);

        var beforeHash = AudioContentHasher.Compute(before, 9, 9 + 17);
        var afterHash = AudioContentHasher.Compute(after, 18, 18 + 17);

        Assert.Equal(beforeHash, afterHash);
    }

    [Fact]
    public void ChangingTheAudioChangesTheHash()
    {
        var one = Write("one", "HEADER"u8, "AUDIO-AUDIO-AUDIO"u8, "TRAILER"u8);
        var other = Write("other", "HEADER"u8, "AUDIO-CHANGED-XXX"u8, "TRAILER"u8);

        Assert.NotEqual(
            AudioContentHasher.Compute(one, 6, 6 + 17),
            AudioContentHasher.Compute(other, 6, 6 + 17));
    }

    [Fact]
    public void UnknownEndPosition_HashesToTheEndOfTheFile()
    {
        var file = Write("unknown", "HEADER"u8, "AUDIO"u8);

        // TagLib reporting nothing useful must not mean hashing nothing, which would give every
        // such file the same hash and collapse them onto one row.
        var hash = AudioContentHasher.Compute(file, 6, 0);

        Assert.NotEmpty(hash);
        Assert.Equal(AudioContentHasher.Compute(file, 6, 11), hash);
    }

    [Fact]
    public void PositionsBeyondTheFile_AreClamped()
    {
        var file = Write("short", default, "AUDIO"u8);

        var hash = AudioContentHasher.Compute(file, 0, long.MaxValue);

        Assert.NotEmpty(hash);
    }

    [Fact]
    public void TwoFilesDifferingOnlyInTheMiddle_StillDifferByLengthWhenTheyDiffer()
    {
        // Sampling reads the ends, so a difference confined to the middle of a long file is only
        // caught when the length differs too. Asserted so the trade-off is deliberate and visible.
        var a = WriteLong('a', middle: 'x');
        var b = WriteLong('a', middle: 'y');

        Assert.Equal(AudioContentHasher.Compute(a, 0, a.Length), AudioContentHasher.Compute(b, 0, b.Length));
    }

    [Fact]
    public void DifferentLengths_NeverCollide()
    {
        var a = WriteLong('a', middle: 'x');
        var b = WriteLong('a', middle: 'x', extraBytes: 1);

        Assert.NotEqual(AudioContentHasher.Compute(a, 0, a.Length), AudioContentHasher.Compute(b, 0, b.Length));
    }

    [Fact]
    public void DifferentStarts_NeverCollide()
    {
        var a = WriteLong('a', middle: 'x');
        var b = WriteLong('b', middle: 'x');

        Assert.NotEqual(AudioContentHasher.Compute(a, 0, a.Length), AudioContentHasher.Compute(b, 0, b.Length));
    }

    [Fact]
    public void Aiff_AnId3ChunkInFrontOfTheSamples_IsWalkedPast()
    {
        // TagLib writes the ID3 chunk after the samples, but not every tagger does, and an
        // odd-length chunk carries a pad byte its length leaves out. Either way the walk has to
        // land on the same samples.
        var (shortTag, _) = WriteAiff("short-tag", "AIFF"u8, id3Length: 7, "SAMPLES-SAMPLES"u8);
        var (longTag, _) = WriteAiff("long-tag", "AIFF"u8, id3Length: 300, "SAMPLES-SAMPLES"u8);

        Assert.Equal(
            AudioContentHasher.Compute(shortTag, AudioFormat.Aif, -1, -1),
            AudioContentHasher.Compute(longTag, AudioFormat.Aif, -1, -1));
    }

    [Fact]
    public void Aiff_OnlyTheSamplesAreHashed()
    {
        // AIFF-C, with alignment bytes the SSND offset says to skip and a chunk after the samples:
        // the range is the samples to the byte, whatever TagLib said it was.
        var (file, samples) = WriteAiff("aifc", "AIFC"u8, id3Length: 5, "SAMPLES-SAMPLES"u8, alignment: 4);

        Assert.Equal(
            AudioContentHasher.Compute(file, samples, samples + 15),
            AudioContentHasher.Compute(file, AudioFormat.Aif, 0, 12));
    }

    /// <summary>An AIFF laid out by hand: COMM, then ID3, then SSND, then an annotation.</summary>
    /// <returns>The file, and where its first sample is.</returns>
    private (IFileInfo File, long Samples) WriteAiff(string name, ReadOnlySpan<byte> formType, int id3Length,
        ReadOnlySpan<byte> samples, int alignment = 0)
    {
        using var body = new MemoryStream();
        body.Write(formType);
        Chunk(body, "COMM"u8, new byte[18]);
        Chunk(body, "ID3 "u8, [.. Enumerable.Repeat((byte)'T', id3Length)]);

        var sound = new byte[8 + alignment + samples.Length];
        BinaryPrimitives.WriteUInt32BigEndian(sound, (uint)alignment);
        Array.Fill(sound, (byte)0xEE, 8, alignment);
        samples.CopyTo(sound.AsSpan(8 + alignment));
        // Past the FORM header, the chunks so far, the SSND chunk's own header, and its offset and
        // block size.
        var first = 8 + body.Length + 8 + 8 + alignment;
        Chunk(body, "SSND"u8, sound);

        Chunk(body, "ANNO"u8, "an annotation after the samples"u8.ToArray());

        var bytes = new byte[8 + body.Length];
        "FORM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), (uint)body.Length);
        body.ToArray().CopyTo(bytes, 8);

        var path = $"/hash/{name}.aiff";
        _fileSystem.File.WriteAllBytes(path, bytes);
        return (_fileSystem.FileInfo.New(path), first);

        static void Chunk(MemoryStream into, ReadOnlySpan<byte> id, byte[] data)
        {
            Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(size, (uint)data.Length);
            into.Write(id);
            into.Write(size);
            into.Write(data);
            if (data.Length % 2 == 1)
            {
                into.WriteByte(0);
            }
        }
    }

    /// <summary>A file long enough that the hasher samples its ends rather than reading it whole.</summary>
    private IFileInfo WriteLong(char edge, char middle, int extraBytes = 0)
    {
        var path = $"/hash/{edge}{middle}{extraBytes}.bin";
        var bytes = new byte[(1024 * 1024) + extraBytes];
        Array.Fill(bytes, (byte)middle);
        Array.Fill(bytes, (byte)edge, 0, 512 * 1024);
        Array.Fill(bytes, (byte)edge, bytes.Length - (512 * 1024), 512 * 1024);
        _fileSystem.File.WriteAllBytes(path, bytes);
        return _fileSystem.FileInfo.New(path);
    }

    private IFileInfo Write(string name, ReadOnlySpan<byte> header, ReadOnlySpan<byte> audio,
        ReadOnlySpan<byte> trailer = default)
    {
        var path = $"/hash/{name}.bin";
        var bytes = new byte[header.Length + audio.Length + trailer.Length];
        header.CopyTo(bytes);
        audio.CopyTo(bytes.AsSpan(header.Length));
        trailer.CopyTo(bytes.AsSpan(header.Length + audio.Length));
        _fileSystem.File.WriteAllBytes(path, bytes);
        return _fileSystem.FileInfo.New(path);
    }
}
