using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks;
using TagLib;
using File = System.IO.File;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// Reading a real file and reporting what it says about itself.
/// </summary>
/// <remarks>
/// On disk rather than on a mock, because the whole job of this class is opening a file with
/// TagLib: a substituted filesystem would prove that the arguments were passed on and nothing else.
/// The audio is the repository's own smoke-test files, copied into a temporary tree and tagged
/// there.
/// </remarks>
public sealed class TrackDiscoveryServiceTests : IDisposable
{
    private readonly FileSystem _fileSystem = new();
    private readonly IDirectoryInfo _root;
    private readonly TrackDiscoveryService _sut = new();

    public TrackDiscoveryServiceTests()
    {
        _root = _fileSystem.DirectoryInfo.New(
            Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
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
        using (var taggable = TagLib.File.Create(file.FullName))
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
        var notAudio = _fileSystem.FileInfo.New(Path.Combine(_root.FullName, "sleeve-notes.txt"));

        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Gather(notAudio, _root));
    }

    // --- Where the file sits ---

    [Fact]
    public void Gather_PathSegments_AreTheFoldersBetweenTheRootAndTheFile_OutermostFirst()
    {
        var file = Audio(Path.Combine("Naragonia", "Idem", "01.mp3"));

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
        var elsewhere = _fileSystem.DirectoryInfo.New(Path.Combine(_root.FullName, "not-the-library"));
        elsewhere.Create();

        var evidence = _sut.Gather(Audio(Path.Combine("Naragonia", "01.mp3")), elsewhere);

        Assert.Empty(evidence.PathSegments);
    }

    [Fact]
    public void Gather_ATrailingSeparatorOnTheRoot_IsStillTheSameRoot()
    {
        var withSeparator = _fileSystem.DirectoryInfo.New(_root.FullName + Path.DirectorySeparatorChar);

        Assert.Equal(["Naragonia"], _sut.Gather(Audio(Path.Combine("Naragonia", "01.mp3")), withSeparator).PathSegments);
    }

    // --- When the file will not be read ---

    [Fact]
    public void Gather_AFileThatIsNoLongerThere_SurfacesAsAnIOException()
    {
        // The watcher and the scanner race with the user's file manager, so a file vanishing
        // mid-scan is ordinary rather than exceptional.
        var gone = _fileSystem.FileInfo.New(Path.Combine(_root.FullName, "gone.mp3"));

        Assert.ThrowsAny<IOException>(() => _sut.Gather(gone, _root));
    }

    [Fact]
    public void Gather_SomethingThatIsNotAudio_IsReportedAgainstItsName()
    {
        // TagLib's own exception says nothing a user could act on, and the scan reports one line
        // per file that would not read, so the name has to be in it.
        var path = Path.Combine(_root.FullName, "corrupt.mp3");
        File.WriteAllText(path, "this is not audio");

        var exception = Assert.Throws<IOException>(() => _sut.Gather(_fileSystem.FileInfo.New(path), _root));

        Assert.Contains("corrupt.mp3", exception.Message, StringComparison.Ordinal);
    }

    // --- Fixtures ---

    /// <summary>The embedded smoke-test audio, written into the temporary tree at a relative path.</summary>
    private IFileInfo Audio(string relativePath, Action<Tag>? tag = null, string resource = "scale.mp3")
    {
        var path = Path.Combine(_root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var source = typeof(TrackDiscoveryServiceTests).Assembly.GetManifestResourceStream(resource)
                            ?? throw new InvalidOperationException($"Embedded audio '{resource}' is missing."))
        using (var destination = File.Create(path))
        {
            source.CopyTo(destination);
        }

        if (tag is not null)
        {
            using var taggable = TagLib.File.Create(path);
            tag(taggable.Tag);
            taggable.Save();
        }

        return _fileSystem.FileInfo.New(path);
    }

    public void Dispose()
    {
        if (_root.Exists)
        {
            _root.Delete(recursive: true);
        }
    }
}
