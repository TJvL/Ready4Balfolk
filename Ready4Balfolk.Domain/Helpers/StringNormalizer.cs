using System.Buffers;
using System.Globalization;
using System.Text;

namespace Ready4Balfolk.Domain.Helpers;

public static class StringNormalizer
{
    // An apostrophe joins a word rather than separating one, so it is removed instead of becoming
    // a space: "Kost ar c'hoad" has to match the "Kost ar choad" people actually type. Every
    // other punctuation mark separates, so a hyphen becomes a space and "Pilé-menu" matches
    // "Pile menu". Getting either backwards silently costs real matches.
    private static readonly SearchValues<char> WordJoiners = SearchValues.Create("'’ʼ´`");

    /// <summary>
    /// Normalizes a string for comparison: no accents, no case, no punctuation.
    /// </summary>
    /// <remarks>
    /// BigBalfolkList's <c>convert.py</c> folds names with the same three rules. The two have to
    /// agree exactly, or a dance name matches in the app and not in the list it ships.
    /// </remarks>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Normalize to decomposed form (separates base characters from diacritics)
        var normalized = input.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark
                || WordJoiners.Contains(c))
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        // Normalize whitespace (collapse multiple spaces, trim)
        return string.Join(" ", sb.ToString().Split([' '], StringSplitOptions.RemoveEmptyEntries));
    }
}
