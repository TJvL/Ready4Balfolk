namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public interface IAudioFile : IDisposable
{
    string Title { get; }
    string Genre { get; }
    string Album { get; }
    string Artist { get; }
    uint Track { get; }
    uint Year { get; }
    string? Dance { get; }
}
