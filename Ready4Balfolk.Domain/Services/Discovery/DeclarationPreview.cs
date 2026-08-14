using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>What one pattern would do to a library, before anyone agrees to it.</summary>
public sealed record PatternPreviewRow(string FileName, string? Dance, string? Artist, string? Title);

/// <summary>The blast radius of a pattern, in the numbers a person needs to greenlight it.</summary>
public sealed record PatternPreview
{
    public required string Pattern { get; init; }

    /// <summary>Why the pattern will not run, when it will not.</summary>
    public PatternProblem Problem { get; init; }

    public int Total { get; init; }

    public int Matched { get; init; }

    public int Missed => Total - Matched;

    /// <summary>A sample of what it makes of the files it matches.</summary>
    public IReadOnlyList<PatternPreviewRow> Matches { get; init; } = [];

    /// <summary>A sample of the names it does not match, which is the pile that would be left.</summary>
    public IReadOnlyList<string> Misses { get; init; } = [];
}

/// <summary>What one folder level actually holds, so a role can be given to it knowingly.</summary>
public sealed record FolderLevelPreview
{
    public required int Level { get; init; }

    public int Total { get; init; }

    /// <summary>How many files are deep enough for this level to exist at all.</summary>
    public int FilesAtThisDepth { get; init; }

    /// <summary>The distinct values at this level, commonest first, with how many files each holds.</summary>
    public IReadOnlyList<(string Value, int Files)> Values { get; init; } = [];
}

/// <summary>How much of the library the declared rules account for, all together.</summary>
public sealed record CoveragePreview
{
    public int Total { get; init; }

    public int Matched { get; init; }

    public int Missed => Total - Matched;
}

/// <summary>
/// Measures a declaration against the library before it is given, because the greenlight has to be
/// an informed one.
/// </summary>
/// <remarks>
/// A declaration approves every track it matches in one act, which is the only way 2685 files get
/// answered by a person in an evening. The price of that is that the person has to be shown the
/// blast radius first: how many it hits, what it makes of them, and what would be left over.
/// </remarks>
public static class DeclarationPreview
{
    /// <summary>Enough rows to see a pattern working, few enough to read.</summary>
    public const int DefaultSampleSize = 20;

    /// <summary>What a pattern would make of a library.</summary>
    /// <param name="pattern">The pattern as the user is writing it.</param>
    /// <param name="fileNames">Every file name in the library, extension and all.</param>
    /// <param name="sampleSize">How many examples to keep of each outcome.</param>
    public static PatternPreview ForPattern(
        string? pattern, IReadOnlyList<string> fileNames, int sampleSize = DefaultSampleSize)
    {
        var (compiled, problem) = FileNamePattern.Parse(pattern);
        if (compiled is null)
        {
            return new PatternPreview
            {
                Pattern = pattern ?? string.Empty,
                Problem = problem,
                Total = fileNames.Count
            };
        }

        var matched = 0;
        var matches = new List<PatternPreviewRow>();
        var misses = new List<string>();

        foreach (var fileName in fileNames)
        {
            var subject = compiled.UsesExtension ? fileName : WithoutExtension(fileName);
            var match = compiled.Match(subject);

            if (match is null)
            {
                if (misses.Count < sampleSize)
                {
                    misses.Add(fileName);
                }

                continue;
            }

            matched++;
            if (matches.Count < sampleSize)
            {
                matches.Add(new PatternPreviewRow(fileName, match.Dance, match.Artist, match.Title));
            }
        }

        return new PatternPreview
        {
            Pattern = compiled.Text,
            Total = fileNames.Count,
            Matched = matched,
            Matches = matches,
            Misses = misses
        };
    }

    /// <summary>What sits at one folder level, so "level 1 is the artist" can be checked by eye.</summary>
    /// <param name="level">Counted from 1 outermost.</param>
    /// <param name="folders">The folders between the music directory and each file, outermost first.</param>
    /// <param name="sampleSize">How many of the level's values to keep.</param>
    public static FolderLevelPreview ForFolderLevel(
        int level, IReadOnlyList<IReadOnlyList<string>> folders, int sampleSize = DefaultSampleSize)
    {
        var atDepth = folders.Where(segments => segments.Count >= level).ToList();

        var values = atDepth
            .GroupBy(segments => segments[level - 1], StringComparer.Ordinal)
            .Select(group => (Value: group.Key, Files: group.Count()))
            .OrderByDescending(entry => entry.Files)
            .ThenBy(entry => entry.Value, StringComparer.Ordinal)
            .Take(sampleSize)
            .ToList();

        return new FolderLevelPreview
        {
            Level = level,
            Total = folders.Count,
            FilesAtThisDepth = atDepth.Count,
            Values = values
        };
    }

    /// <summary>How many files the whole ordered set of patterns accounts for between them.</summary>
    /// <remarks>
    /// The number that matters after a rule is greenlit, because what it does not match is the queue
    /// a person still has to work through, and that is what the next declaration is aimed at.
    /// </remarks>
    public static CoveragePreview ForPatterns(DiscoverySettings settings, IReadOnlyList<string> fileNames)
    {
        var declared = DeclaredDiscovery.Compile(settings);
        if (declared.Patterns.Count == 0)
        {
            return new CoveragePreview { Total = fileNames.Count };
        }

        var matched = fileNames.Count(fileName =>
            declared.MatchFileName(fileName, WithoutExtension(fileName)) is not null);

        return new CoveragePreview { Total = fileNames.Count, Matched = matched };
    }

    /// <summary>The names not accounted for, which is what a later rule would be measured against.</summary>
    public static IReadOnlyList<string> Leftovers(DiscoverySettings settings, IReadOnlyList<string> fileNames)
    {
        var declared = DeclaredDiscovery.Compile(settings);

        return declared.Patterns.Count == 0
            ? fileNames
            : [.. fileNames.Where(fileName => declared.MatchFileName(fileName, WithoutExtension(fileName)) is null)];
    }

    private static string WithoutExtension(string fileName) => Path.GetFileNameWithoutExtension(fileName);
}
