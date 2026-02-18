using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public readonly struct TrackViewModel(Track track)
{
    public Track Track => track;
    public string Dance => track.Dance;
    public string Artist => track.Artist;
    public string Title => track.Title;
    public AudioFormat Format => track.Format;
    public string LengthFormatted => $"{(int)track.Length.TotalMinutes}:{track.Length.Seconds:D2}";
}
