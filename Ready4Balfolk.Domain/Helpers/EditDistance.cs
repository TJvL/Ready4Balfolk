namespace Ready4Balfolk.Domain.Helpers;

/// <summary>Levenshtein distance, for deciding whether one string is a misspelling of another.</summary>
public static class EditDistance
{
    /// <summary>
    /// The number of single-character edits between two strings, stopping early once it is clear
    /// the answer is over <paramref name="limit"/>.
    /// </summary>
    /// <remarks>
    /// The limit is not just an optimisation: a value is only offered as a misspelling when it is
    /// close, so anything past the limit needs no exact answer.
    /// </remarks>
    public static int Between(string left, string right, int limit = int.MaxValue)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 0;
        }

        if (left.Length == 0 || right.Length == 0)
        {
            return Math.Max(left.Length, right.Length);
        }

        if (Math.Abs(left.Length - right.Length) > limit)
        {
            return limit + 1;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            var best = current[0];

            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
                best = Math.Min(best, current[j]);
            }

            if (best > limit)
            {
                return limit + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
