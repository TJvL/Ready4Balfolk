using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks;
using TagLib;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Reading a real file and reporting what it says about itself.
/// </summary>
/// <remarks>
/// The bytes are real audio, the repository's own smoke-test files, and TagLib really parses them;
/// only the disk is a mock. That holds only because the service opens the file through the file
/// system it was found on, so every test here would fail if a read went to the real disk instead.
/// </remarks>
public sealed class TrackDiscoveryServiceTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IDirectoryInfo _root;
    private readonly TrackDiscoveryService _sut = new();

    public TrackDiscoveryServiceTests()
    {
        _root = _fileSystem.DirectoryInfo.New(_fileSystem.Path.GetFullPath("/music"));
        _root.Create();
    }

    // --- What the tags say ---

    [Fact]
    public void Gather_ReadsWhatTheTagsSay()
    {
        var file = Audio("tagged.mp3", tag =>
        {
            tag.Title = "Salamandre";
            tag.Performers = ["Naragonia"];
            tag.AlbumArtists = ["Naragonia"];
            tag.Album = "Idem";
            tag.Comment = "Mazurka";
        });

        var evidence = _sut.Gather(file, _root);

        Assert.Equal("tagged.mp3", evidence.FileName);
        Assert.Equal("Salamandre", evidence.TagTitle);
        Assert.Equal("Naragonia", evidence.TagArtist);
        Assert.Equal("Naragonia", evidence.TagAlbumArtist);
        Assert.Equal("Idem", evidence.TagAlbum);
        Assert.Equal("Mazurka", evidence.TagComment);
    }

    [Fact]
    public void Gather_ReadsCustomTags()
    {
        // The declared dance tag is the strongest claim a file can make, so it has to survive the
        // trip out of the file whatever the tagger called it.
        var file = Audio("custom.mp3");
        using (var taggable = TagLib.File.Create(new TagLibFileAbstraction(file)))
        {
            var id3v2 = (TagLib.Id3v2.Tag)taggable.GetTag(TagTypes.Id3v2, true);
            TagLib.Id3v2.UserTextInformationFrame.Get(id3v2, "DANCE", true).Text = ["Mazurka"];
            taggable.Save();
        }

        var evidence = _sut.Gather(file, _root);

        Assert.Equal("Mazurka", evidence.CustomTags["dance"]);
    }

    [Fact]
    public void Gather_AnUntaggedFile_ReportsNothingRatherThanGuessing()
    {
        var evidence = _sut.Gather(Audio("plain.mp3"), _root);

        Assert.Null(evidence.TagTitle);
        Assert.Null(evidence.TagArtist);
        Assert.Empty(evidence.CustomTags);
    }

    // --- What the file itself says ---

    [Fact]
    public void Gather_ReportsTheDurationAndTheFormat()
    {
        var evidence = _sut.Gather(Audio("scale.mp3"), _root);

        Assert.Equal(AudioFormat.Mp3, evidence.Format);
        Assert.True(evidence.Duration > TimeSpan.Zero);
    }

    [Fact]
    public void Gather_ReportsAContentHash() =>
        // What the index recognises a moved or retagged file by, so an empty one would collapse
        // every file onto a single row.
        Assert.NotEmpty(_sut.Gather(Audio("hashed.mp3"), _root).ContentHash);

    // --- What the index recognises the file by ---

    [Theory]
    [InlineData("scale.mp3")]
    [InlineData("scale.mp2")]
    [InlineData("scale.ogg")]
    [InlineData("scale.wav")]
    [InlineData("scale.flac")]
    [InlineData("scale.aiff")]
    public void Gather_RetaggingAFile_KeepsItsContentHash(string resource)
    {
        // The hash is the track's row in the index, and its approvals hang off it, so a tag edit
        // made in another program must not move it. FLAC hashed its metadata blocks and a tagged
        // AIFF hashed its ID3 chunk, so fixing a title sent an approved track back to review.
        var file = Audio("retagged" + Path.GetExtension(resource), resource: resource);
        var untagged = _sut.Gather(file, _root).ContentHash;

        Retag(file, tag =>
        {
            tag.Title = "Salamandre";
            tag.Performers = ["Naragonia"];
            tag.Comment = "Mazurka";
        });
        var tagged = _sut.Gather(file, _root).ContentHash;

        // Longer, and with a cover, so the tags outgrow whatever room they had.
        Retag(file, tag =>
        {
            tag.Title = "Salamandre, the long version from the second set";
            tag.Album = "Idem";
            ByteVector cover = [.. new byte[16 * 1024]];
            tag.Pictures = [new Picture(cover)];
        });
        var retagged = _sut.Gather(file, _root).ContentHash;

        Assert.Equal(untagged, tagged);
        Assert.Equal(untagged, retagged);
    }

    [Fact]
    public void Gather_AFlacWithId3TagsAroundIt_HashesTheSameAsWithout()
    {
        // Some taggers put an ID3v2 tag in front of a FLAC stream and an ID3v1 tag behind it, and
        // write the Vorbis comment as well. None of it is audio, so none of it may move the hash.
        var file = Audio("id3.flac", resource: "scale.flac");
        var plain = _sut.Gather(file, _root).ContentHash;

        using (var taggable = TagLib.File.Create(new TagLibFileAbstraction(file)))
        {
            taggable.GetTag(TagTypes.Id3v2, true);
            taggable.GetTag(TagTypes.Id3v1, true);
            taggable.Tag.Title = "Salamandre";
            taggable.Save();
        }

        using (var written = TagLib.File.Create(new TagLibFileAbstraction(file)))
        {
            Assert.Equal(TagTypes.Id3v2 | TagTypes.Id3v1, written.TagTypesOnDisk & (TagTypes.Id3v2 | TagTypes.Id3v1));
        }

        Assert.Equal(plain, _sut.Gather(file, _root).ContentHash);
    }

    [Fact]
    public void Gather_TwoAiffRecordingsWithTheSameTags_AreTwoTracks()
    {
        // Hashing a tagged AIFF's ID3 chunk gave two recordings tagged alike one hash, and the
        // content hash is unique in the index, so they became one track.
        var one = Audio("one.aiff", SameTags, "scale.aiff");
        var other = Audio("other.aiff", resource: "scale.aiff");
        var bytes = _fileSystem.File.ReadAllBytes(other.FullName);
        // The same scale played backwards: as long as the first, and sounding nothing like it.
        var samples = bytes.AsSpan().IndexOf("SSND"u8) + 16;
        bytes.AsSpan(samples).Reverse();
        _fileSystem.File.WriteAllBytes(other.FullName, bytes);
        Retag(other, SameTags);

        Assert.NotEqual(_sut.Gather(one, _root).ContentHash, _sut.Gather(other, _root).ContentHash);

        static void SameTags(Tag tag)
        {
            tag.Title = "Scale";
            tag.Performers = ["Nobody"];
        }
    }

    [Theory]
    [InlineData("scale.ogg", "vorbis.ogg", AudioFormat.Ogg)]
    [InlineData("scale.ogg", "vorbis.oga", AudioFormat.Ogg)]
    [InlineData("scale.mp3", "layer2.mp2", AudioFormat.Mp3)]
    public void Gather_ReadsTheFormatFromTheExtension(string resource, string name, AudioFormat expected) =>
        // The aliases matter: a library written by a tagger that prefers .oga is not a library of
        // files this application cannot read.
        Assert.Equal(expected, _sut.Gather(Audio(name, resource: resource), _root).Format);

    [Fact]
    public void Gather_AnExtensionThisApplicationDoesNotPlay_IsRefusedBeforeTheFileIsOpened()
    {
        // The check comes first on purpose: the answer to a .txt in the music folder is that it is
        // not audio, not that it could not be parsed.
        var notAudio = _fileSystem.FileInfo.New(_fileSystem.Path.Combine(_root.FullName, "sleeve-notes.txt"));

        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Gather(notAudio, _root));
    }

    // --- Where the file sits ---

    [Fact]
    public void Gather_PathSegments_AreTheFoldersBetweenTheRootAndTheFile_OutermostFirst()
    {
        var file = Audio(_fileSystem.Path.Combine("Naragonia", "Idem", "01.mp3"));

        var evidence = _sut.Gather(file, _root);

        Assert.Equal(["Naragonia", "Idem"], evidence.PathSegments);
        Assert.Equal("Naragonia/Idem", evidence.FolderKey);
    }

    [Fact]
    public void Gather_AFileInTheRootItself_SitsInNoFolder()
    {
        var evidence = _sut.Gather(Audio("loose.mp3"), _root);

        Assert.Empty(evidence.PathSegments);
        Assert.Null(evidence.FolderKey);
    }

    [Fact]
    public void Gather_AFileOutsideTheMusicDirectory_HasNoPathToRead()
    {
        // Nothing in the path of a file that is not in the library means anything about the
        // library, and walking to the filesystem root would read the user's home directory as
        // dance names.
        var elsewhere = _fileSystem.DirectoryInfo.New(_fileSystem.Path.Combine(_root.FullName, "not-the-library"));
        elsewhere.Create();

        var evidence = _sut.Gather(Audio(_fileSystem.Path.Combine("Naragonia", "01.mp3")), elsewhere);

        Assert.Empty(evidence.PathSegments);
    }

    [Fact]
    public void Gather_ATrailingSeparatorOnTheRoot_IsStillTheSameRoot()
    {
        var withSeparator = _fileSystem.DirectoryInfo.New(_root.FullName + _fileSystem.Path.DirectorySeparatorChar);

        Assert.Equal(["Naragonia"], _sut.Gather(Audio(_fileSystem.Path.Combine("Naragonia", "01.mp3")), withSeparator).PathSegments);
    }

    // --- When the file will not be read ---

    [Fact]
    public void Gather_AFileThatIsNoLongerThere_SurfacesAsAnIOException()
    {
        // The watcher and the scanner race with the user's file manager, so a file vanishing
        // mid-scan is ordinary rather than exceptional.
        var gone = _fileSystem.FileInfo.New(_fileSystem.Path.Combine(_root.FullName, "gone.mp3"));

        Assert.ThrowsAny<IOException>(() => _sut.Gather(gone, _root));
    }

    [Fact]
    public void Gather_SomethingThatIsNotAudio_IsReportedAgainstItsName()
    {
        // TagLib's own exception says nothing a user could act on, and the scan reports one line
        // per file that would not read, so the name has to be in it.
        var path = _fileSystem.Path.Combine(_root.FullName, "corrupt.mp3");
        _fileSystem.AddFile(path, new MockFileData("this is not audio"));

        var exception = Assert.Throws<IOException>(() => _sut.Gather(_fileSystem.FileInfo.New(path), _root));

        Assert.Contains("corrupt.mp3", exception.Message, StringComparison.Ordinal);
    }

    // --- Fixtures ---

    /// <summary>The embedded smoke-test audio, written into the mock tree at a relative path.</summary>
    /// <remarks>
    /// Tagged through the same abstraction the service reads with, so the tags are written to the
    /// mock file and nowhere else.
    /// </remarks>
    private IFileInfo Audio(string relativePath, Action<Tag>? tag = null, string resource = "scale.mp3")
    {
        var path = _fileSystem.Path.Combine(_root.FullName, relativePath);

        using (var source = typeof(TrackDiscoveryServiceTests).Assembly.GetManifestResourceStream(resource)
                            ?? throw new InvalidOperationException($"Embedded audio '{resource}' is missing."))
        using (var copy = new MemoryStream())
        {
            source.CopyTo(copy);
            _fileSystem.AddFile(path, new MockFileData(copy.ToArray()));
        }

        var file = _fileSystem.FileInfo.New(path);

        if (tag is not null)
        {
            Retag(file, tag);
        }

        return file;
    }

    /// <summary>Writes tags into a file the way a tagger would, through the mock file system.</summary>
    private static void Retag(IFileInfo file, Action<Tag> tag)
    {
        using var taggable = TagLib.File.Create(new TagLibFileAbstraction(file));
        tag(taggable.Tag);
        taggable.Save();
    }
}
