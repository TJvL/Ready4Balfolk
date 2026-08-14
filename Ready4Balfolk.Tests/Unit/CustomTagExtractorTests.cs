using Ready4Balfolk.Domain.Helpers;
using TagLib;
using File = TagLib.File;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Against real tag structures, because the frame walking is the part a mock cannot vouch for.
/// </summary>
/// <remarks>
/// The audio is the repository's own smoke-test files, embedded in the test assembly and tagged
/// in memory with TagLib itself: the same library writes and reads, and no test touches a disk.
/// </remarks>
public sealed class CustomTagExtractorTests
{
    [Fact]
    public void AnId3v2CustomFrame_IsReadByItsName()
    {
        var stream = Tagged("scale.mp3", file =>
        {
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2, true);
            TagLib.Id3v2.UserTextInformationFrame.Get(id3v2, "DANCE", true).Text = ["Mazurka"];
        });

        using var reread = Open("scale.mp3", stream);
        var tags = reread.GetCustomTags();

        Assert.Equal("Mazurka", tags["DANCE"]);
        // Case-insensitive, because nobody remembers how their tagger capitalised it.
        Assert.Equal("Mazurka", tags["dance"]);
    }

    [Fact]
    public void AXiphField_IsReadByItsName()
    {
        var stream = Tagged("scale.ogg", file =>
        {
            var xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph, true);
            xiph.SetField("DANCE", "Scottish");
        });

        using var reread = Open("scale.ogg", stream);
        var tags = reread.GetCustomTags();

        Assert.Equal("Scottish", tags["dance"]);
    }

    [Fact]
    public void AFileWithoutCustomTags_YieldsNoneOfTheStandardFields()
    {
        using var untouched = Open("scale.mp3", Audio("scale.mp3"));

        Assert.DoesNotContain("DANCE", untouched.GetCustomTags().Keys);
    }

    /// <summary>The embedded audio with the given tags written into it, still in memory.</summary>
    private static MemoryStream Tagged(string name, Action<File> write)
    {
        var stream = Audio(name);
        using var file = Open(name, stream);
        write(file);
        file.Save();
        return stream;
    }

    private static File Open(string name, MemoryStream stream)
    {
        stream.Position = 0;
        return File.Create(new InMemoryAudio(name, stream));
    }

    private static MemoryStream Audio(string name)
    {
        using var resource = typeof(CustomTagExtractorTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded audio '{name}' is missing.");

        // An expandable copy: writing tags grows the file, and a stream over a byte[] cannot grow.
        var stream = new MemoryStream();
        resource.CopyTo(stream);
        return stream;
    }

    private sealed class InMemoryAudio(string name, MemoryStream stream) : File.IFileAbstraction
    {
        public string Name => name;

        public Stream ReadStream => stream;

        public Stream WriteStream => stream;

        // Left open on purpose: the same stream is reread after saving.
        public void CloseStream(Stream stream)
        {
        }
    }
}
