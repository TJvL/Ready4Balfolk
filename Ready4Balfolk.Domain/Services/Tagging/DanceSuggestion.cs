namespace Ready4Balfolk.Domain.Services.Tagging;

/// <summary>A dance a value might mean, and how much the library thinks so.</summary>
/// <param name="Slug">The dance.</param>
/// <param name="DisplayName">What that dance is currently called.</param>
/// <param name="TrackCount">
/// How many tracks already resolved to it. Ranking by this rather than alphabetically puts the
/// dance the user actually plays at the top.
/// </param>
public sealed record DanceSuggestion(string Slug, string DisplayName, int TrackCount);
