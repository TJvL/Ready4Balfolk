namespace Ready4Balfolk.Domain.Services.Tracks;

public interface IRandomTrackService
{
    Models.Tracks.Track? PickRandomTrack(RandomSelectionScope scope, bool allowDuplicates);
}
