namespace Ready4Balfolk.Domain.Models.Tracks;

public sealed record Track(string Dance, string Artist, string Title, FileInfo FileInfo, TimeSpan Length, AudioFormat Format)
{
    public string OriginalDance { get; init; } = Dance;
}
