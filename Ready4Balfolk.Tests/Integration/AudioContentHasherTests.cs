using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Tests.Integration;

public sealed class AudioContentHasherTests : IDisposable
{
    private readonly DirectoryInfo _tempDir;

    public AudioContentHasherTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
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

    /// <summary>A file long enough that the hasher samples its ends rather than reading it whole.</summary>
    private FileInfo WriteLong(char edge, char middle, int extraBytes = 0)
    {
        var path = Path.Combine(_tempDir.FullName, $"{edge}{middle}{extraBytes}.bin");
        var bytes = new byte[(1024 * 1024) + extraBytes];
        Array.Fill(bytes, (byte)middle);
        Array.Fill(bytes, (byte)edge, 0, 512 * 1024);
        Array.Fill(bytes, (byte)edge, bytes.Length - (512 * 1024), 512 * 1024);
        File.WriteAllBytes(path, bytes);
        return new FileInfo(path);
    }

    public void Dispose()
    {
        if (_tempDir.Exists)
        {
            _tempDir.Delete(true);
        }
    }

    private FileInfo Write(string name, ReadOnlySpan<byte> header, ReadOnlySpan<byte> audio,
        ReadOnlySpan<byte> trailer = default)
    {
        var path = Path.Combine(_tempDir.FullName, $"{name}.bin");
        var bytes = new byte[header.Length + audio.Length + trailer.Length];
        header.CopyTo(bytes);
        audio.CopyTo(bytes.AsSpan(header.Length));
        trailer.CopyTo(bytes.AsSpan(header.Length + audio.Length));
        File.WriteAllBytes(path, bytes);
        return new FileInfo(path);
    }
}
