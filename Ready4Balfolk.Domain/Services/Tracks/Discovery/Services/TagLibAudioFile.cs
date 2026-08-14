using Ready4Balfolk.Domain.Helpers;
using File = TagLib.File;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class TagLibAudioFile(File file) : IAudioFile
{
    public string Title => file.Tag.Title;
    public string Genre => file.Tag.FirstGenre;
    public string Album => file.Tag.Album;
    public string Artist => file.Tag.FirstPerformer;
    public uint Track => file.Tag.Track;
    public uint Year => file.Tag.Year;
    public string? Dance => file.GetCustomTag("dance")?.FirstOrDefault();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        file.Dispose();
    }
}
