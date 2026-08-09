using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Reads a file and reports what it says about itself, deciding nothing.</summary>
/// <remarks>
/// Deciding what a track is belongs to <see cref="TrackInformationResolver"/>, which needs no file
/// and can be re-run whenever the dance list changes. This half is the part that costs an open.
/// </remarks>
public sealed class TrackDiscoveryService : ITrackDiscoveryService
{
    public TrackEvidence Gather(FileInfo fileInfo, DirectoryInfo musicRoot)
    {
        var format = ParseAudioFormat(fileInfo);

        try
        {
            using var file = TagLib.File.Create(fileInfo.FullName);
            var tag = file.Tag;

            var evidence = new TrackEvidence
            {
                FileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name),
                PathSegments = SegmentsBetween(fileInfo, musicRoot),
                TagTitle = tag.Title,
                TagArtist = tag.FirstPerformer,
                TagAlbumArtist = tag.FirstAlbumArtist,
                TagAlbum = tag.Album,
                TagComment = tag.Comment,
                Duration = file.Properties.Duration,
                Format = format,
                ContentHash = AudioContentHasher.Compute(
                    fileInfo, file.InvariantStartPosition, file.InvariantEndPosition)
            };

            return evidence;
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new IOException($"Unable to read '{fileInfo.Name}'.", exception);
        }
    }

    /// <summary>The folders between the music directory and the file, outermost first.</summary>
    private static List<string> SegmentsBetween(FileInfo fileInfo, DirectoryInfo musicRoot)
    {
        var segments = new List<string>();
        var directory = fileInfo.Directory;
        var rootPath = Path.TrimEndingDirectorySeparator(musicRoot.FullName);

        while (directory is not null
               && !string.Equals(Path.TrimEndingDirectorySeparator(directory.FullName), rootPath, StringComparison.Ordinal))
        {
            segments.Add(directory.Name);
            directory = directory.Parent;
        }

        // A file outside the music directory entirely: nothing about its path means anything here.
        if (directory is null)
        {
            return [];
        }

        segments.Reverse();
        return segments;
    }

    private static AudioFormat ParseAudioFormat(FileInfo fileInfo)
    {
        var ext = fileInfo.Extension;
        return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mp2", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mp1", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Mp3
            : ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Wav
            : ext.Equals(".flac", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".fla", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Flac
            : ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".oga", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Ogg
            : ext.Equals(".aif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".aiff", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Aif
            : throw new ArgumentOutOfRangeException(nameof(fileInfo), ext,
                $"Unsupported audio format for '{fileInfo.Name}'.");
    }
}
