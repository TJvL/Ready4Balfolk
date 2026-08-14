using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public interface ITagFileFactory
{
    IAudioFile Create(IFileInfo fileInfo);
}
