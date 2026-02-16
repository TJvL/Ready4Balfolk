using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Editor;

public static class DanceSynonymTransforms
{
    public static bool IsNameUnique(
        IReadOnlyList<DanceMainName> list, string name,
        int? excludeMainIndex = null, int? excludeSynonymIndex = null)
    {
        var normalized = StringNormalizer.Normalize(name);

        for (var i = 0; i < list.Count; i++)
        {
            var main = list[i];

            if (!(excludeMainIndex == i && excludeSynonymIndex is null) &&
                StringNormalizer.Normalize(main.Name).Equals(normalized, StringComparison.Ordinal))
            {
                return false;
            }

            var synonyms = main.Synonyms.ToList();
            if (synonyms.Where((_, j) => excludeMainIndex != i || excludeSynonymIndex != j).Any(t => StringNormalizer.Normalize(t.Name).Equals(normalized, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<DanceMainName> AddMainName(IReadOnlyList<DanceMainName> list)
        => [.. list, new(DomainStrings.DanceSynonymTransforms_NewDance, [])];

    public static IReadOnlyList<DanceMainName> DeleteMainName(IEnumerable<DanceMainName> list, int index)
        => list.Where((_, i) => i != index).ToList();

    public static IReadOnlyList<DanceMainName> RenameMainName(
        IEnumerable<DanceMainName> list, int index, string newName)
        => list.Select((m, i) => i == index
            ? m with
            {
                Name = newName
            }
            : m).ToList();

    public static IReadOnlyList<DanceMainName> AddSynonym(
        IEnumerable<DanceMainName> list, int mainNameIndex)
        => list.Select((m, i) => i == mainNameIndex
            ? m with
            {
                Synonyms = [.. m.Synonyms, new DanceSynonym(DomainStrings.DanceSynonymTransforms_NewSynonym)]
            }
            : m).ToList();

    public static IReadOnlyList<DanceMainName> AddSynonymWithName(
        IEnumerable<DanceMainName> list, int mainNameIndex, string name)
        => list.Select((m, i) => i == mainNameIndex
            ? m with
            {
                Synonyms = [.. m.Synonyms, new DanceSynonym(name)]
            }
            : m).ToList();

    public static IReadOnlyList<DanceMainName> DeleteSynonym(
        IEnumerable<DanceMainName> list, int mainNameIndex, int synonymIndex)
        => list.Select((m, i) => i == mainNameIndex
            ? m with
            {
                Synonyms = m.Synonyms.Where((_, si) => si != synonymIndex).ToList()
            }
            : m).ToList();

    public static IReadOnlyList<DanceMainName> RenameSynonym(
        IEnumerable<DanceMainName> list, int mainNameIndex, int synonymIndex, string newName)
        => list.Select((m, i) => i == mainNameIndex
            ? m with
            {
                Synonyms = m.Synonyms.Select((s, si) => si == synonymIndex
                    ? new DanceSynonym(newName)
                    : s).ToList()
            }
            : m).ToList();
}
