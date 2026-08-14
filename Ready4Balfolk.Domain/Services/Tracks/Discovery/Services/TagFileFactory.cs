using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class TagFileFactory : ITagFileFactory
{
    public IAudioFile Create(IFileInfo fileInfo) => new TagLibAudioFile(TagLib.File.Create(fileInfo.FullName));
}
