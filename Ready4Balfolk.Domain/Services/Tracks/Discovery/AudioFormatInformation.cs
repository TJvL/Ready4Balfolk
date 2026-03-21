using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public static class AudioFormatInformation
{
    private static readonly Dictionary<string, AudioFormat> SupportedFormatLookup = new Dictionary<string, AudioFormat>()
    {
        { ".mp1", AudioFormat.Mp3 },
        { ".mp2", AudioFormat.Mp3 },
        { ".mp3", AudioFormat.Mp3 },
        { ".wav", AudioFormat.Wav },
        { ".flac", AudioFormat.Flac },
        { ".fla", AudioFormat.Flac },
        { ".ogg", AudioFormat.Ogg },
        { ".oga", AudioFormat.Ogg },
        { ".aif", AudioFormat.Aif },
        { ".aiff", AudioFormat.Aif },
    };

    public static HashSet<string> SupportedFormats => [.. SupportedFormatLookup.Keys];

    public static AudioFormat ParseAudioFormat(string extension)
    {
        if (!SupportedFormatLookup.TryGetValue(extension.ToLowerInvariant(), out var format))
        {
            throw new ArgumentOutOfRangeException(nameof(extension), extension, $"Unsupported audio format for '{extension}'.");
        }

        return format;
    }
}
