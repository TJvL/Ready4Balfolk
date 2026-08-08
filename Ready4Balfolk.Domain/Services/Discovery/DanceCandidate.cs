namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>One suggestion of which dance a track is, and what suggested it.</summary>
public sealed record DanceCandidate(string Slug, DanceEvidenceSource Source, string MatchedName);
