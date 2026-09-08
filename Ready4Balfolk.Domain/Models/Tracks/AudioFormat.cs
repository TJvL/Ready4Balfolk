namespace Ready4Balfolk.Domain.Models.Tracks;

/// <summary>What kind of audio a file holds.</summary>
/// <remarks>
/// The numbers are written into the library index and are the identity of the member, not its
/// position. They are pinned so a member added here can never be added in front of one: a scan
/// rebuilds an unchanged file's row out of the index rather than opening it again, so a shift
/// would leave every FLAC in the library reading as an Ogg and no later scan would put it back.
/// </remarks>
public enum AudioFormat
{
    Mp3 = 0,

    Wav = 1,

    Flac = 2,

    Ogg = 3,

    Aif = 4
}
