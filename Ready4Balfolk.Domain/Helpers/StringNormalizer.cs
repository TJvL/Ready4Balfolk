using System.Globalization;
using System.Text;

namespace Ready4Balfolk.Domain.Helpers;

public static class StringNormalizer
{
    /// <summary>
    /// Normalizes a string by removing accents, special characters, and converting to lowercase.
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Normalize to decomposed form (separates base characters from diacritics)
        var normalized = input.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();
        foreach (var c in from c in normalized
                          let category = CharUnicodeInfo.GetUnicodeCategory(c)
                          where category != UnicodeCategory.NonSpacingMark
                          where char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)
                          select c)
        {
            sb.Append(char.ToLowerInvariant(c));
        }

        // Normalize whitespace (collapse multiple spaces, trim)
        return string.Join(" ", sb.ToString().Split([' '], StringSplitOptions.RemoveEmptyEntries));
    }
}
