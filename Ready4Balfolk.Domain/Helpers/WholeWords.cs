namespace Ready4Balfolk.Domain.Helpers;

/// <summary>Whether one folded text appears in another as whole words, not as a substring.</summary>
/// <remarks>
/// A substring is the wrong question for names: "Ron" sits inside "Rond de Landéda" and "Tour"
/// inside "Tournai", and neither is that name. Both sides are expected folded the same way
/// (match keys), so a word is whatever lies between two spaces.
/// </remarks>
public static class WholeWords
{
    /// <summary>
    /// Whether the needle's words appear in the text as a run of whole words, so "bourree" is in
    /// "bourree 3" and in "bourree" itself, but "tour" is not in "valse tournai".
    /// </summary>
    public static bool Contains(string foldedText, string foldedNeedle)
    {
        var text = foldedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needle = foldedNeedle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (needle.Length == 0 || needle.Length > text.Length)
        {
            return false;
        }

        for (var start = 0; start + needle.Length <= text.Length; start++)
        {
            var matches = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (!string.Equals(text[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
