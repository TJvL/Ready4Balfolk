using Ready4Balfolk.Domain.Helpers;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>
/// The two word lists the published file carries, and the match key they produce.
/// </summary>
/// <remarks>
/// <para>
/// Grammar is not a spelling. <c>Bourrée à 3 temps</c>, <c>Bourrée in 3</c>, <c>Bourrée à trois
/// temps</c>, <c>Bourrée 3t</c> and <c>Bourrée 3</c> are one name written five ways, and a list that
/// wrote all five out would be unreadable. So a number word becomes its number, glue is dropped, and
/// what is left is the key a name is compared on.
/// </para>
/// <para>
/// The lists ship in the file rather than in this code because they grow with every language
/// somebody adds a name in, and a copy of the file brings the new words with it.
/// </para>
/// </remarks>
public sealed record DanceWords
{
    public static readonly DanceWords None = new();

    /// <summary>Glue that says nothing: articles, prepositions, and "temps" and its translations.</summary>
    public IReadOnlySet<string> Ignored { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Number words in every language the list has met, folded, to their digits.</summary>
    public IReadOnlyDictionary<string, string> Numbers { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static DanceWords From(DanceList list) => new()
    {
        Ignored = new HashSet<string>(
            list.IgnoredWords.Select(StringNormalizer.Normalize).Where(word => word.Length > 0),
            StringComparer.Ordinal),
        Numbers = list.NumberWords
            .Select(pair => (Word: StringNormalizer.Normalize(pair.Key), Number: pair.Value.Trim()))
            .Where(pair => pair.Word.Length > 0 && pair.Number.Length > 0)
            .GroupBy(pair => pair.Word, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Number, StringComparer.Ordinal)
    };

    /// <summary>
    /// What a name is compared on: folded, numbers as digits, glue dropped.
    /// </summary>
    /// <remarks>
    /// A name that is nothing but glue keeps its folded form, or "In de" would key to nothing at all
    /// and every such name would be the same name.
    /// </remarks>
    public string KeyFor(string? name)
    {
        var folded = StringNormalizer.Normalize(name ?? string.Empty);
        if (folded.Length == 0)
        {
            return string.Empty;
        }

        var kept = folded
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(AsNumber)
            .Where(word => !Ignored.Contains(word))
            .ToList();

        return kept.Count == 0 ? folded : string.Join(' ', kept);
    }

    /// <summary>The word as its digits, when the list knows it as a number word.</summary>
    public string AsNumber(string word) => Numbers.GetValueOrDefault(word, word);

    /// <summary>True when a word carries nothing and may be stepped over while matching.</summary>
    public bool IsGlue(string word) => Ignored.Contains(word);
}
