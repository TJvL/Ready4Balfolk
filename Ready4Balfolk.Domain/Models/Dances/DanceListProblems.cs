namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>What is wrong with a dance list, as separate lists so a message can name the offenders.</summary>
public sealed record DanceListProblems(
    IReadOnlyList<string> DuplicateNames,
    IReadOnlyList<string> DuplicateSlugs,
    IReadOnlyList<string> SlugsWithoutNames,
    IReadOnlyList<string> UnnamedCategories)
{
    public static DanceListProblems None { get; } = new([], [], [], []);

    public bool Any =>
        DuplicateNames.Count > 0
        || DuplicateSlugs.Count > 0
        || SlugsWithoutNames.Count > 0
        || UnnamedCategories.Count > 0;
}
