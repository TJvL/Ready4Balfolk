using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>One file as calibration sees it: a name, the folders above it, and what its tags said.</summary>
/// <remarks>
/// Strings that are already in the index. Calibration opens no audio files and asks nothing of the
/// disk: it is arithmetic over what a scan has already read.
/// </remarks>
public sealed record CalibrationFile
{
    public required string FileName { get; init; }

    /// <summary>The folders between the music directory and the file, outermost first.</summary>
    public required IReadOnlyList<string> Folders { get; init; }

    /// <summary>What the tags said the artist was, when a tag is what answered it.</summary>
    public string? TagArtist { get; init; }

    /// <summary>What the tags said the title was, when a tag is what answered it.</summary>
    public string? TagTitle { get; init; }
}

/// <summary>What one position of a file name shape turned out to behave like.</summary>
/// <param name="Position">Counted from 1, left to right.</param>
/// <param name="Field">What it is, or null when the signals do not converge.</param>
/// <param name="ConstantInFolders">Folders where every file shares this value.</param>
/// <param name="Folders">Folders holding more than one file of this shape.</param>
/// <param name="Distinct">How many different values it takes.</param>
/// <param name="DanceNames">How many of its values the published list knows.</param>
/// <param name="AgreesWithTag">How many match the tag that would answer the field it looks like.</param>
/// <param name="Files">Files in the shape.</param>
public sealed record PositionFinding(
    int Position,
    TrackField? Field,
    int ConstantInFolders,
    int Folders,
    int Distinct,
    int DanceNames,
    int AgreesWithTag,
    int Files);

/// <summary>A file name shape the library actually has, and what can be said about it.</summary>
public sealed record ShapeProposal
{
    /// <summary>The separator the shape is built on, and how many fields it splits into.</summary>
    public required string Separator { get; init; }

    public required int Fields { get; init; }

    public required int Files { get; init; }

    /// <summary>Out of how many were looked at, so a proportion can be judged rather than a count.</summary>
    public required int Considered { get; init; }

    public required IReadOnlyList<PositionFinding> Positions { get; init; }

    public required IReadOnlyList<string> Samples { get; init; }

    /// <summary>
    /// The rule this shape would be declared as, or null when nothing can be named.
    /// </summary>
    /// <remarks>
    /// A position the signals could not identify becomes <c>%i</c>, which is the honest reading: the
    /// field is there, it is not any of the ones we can name, and the pattern still matches. A shape
    /// where nothing at all could be named is worth showing and not worth declaring.
    /// </remarks>
    public string? Pattern { get; init; }
}

/// <summary>A folder level that behaves like something, with the evidence for saying so.</summary>
public sealed record FolderRoleProposal
{
    public required int Level { get; init; }

    public required FolderRole Role { get; init; }

    /// <summary>Files whose value at this level says what the role claims.</summary>
    public required int Agreeing { get; init; }

    /// <summary>Files deep enough for the level to exist and carrying something to compare with.</summary>
    public required int Considered { get; init; }

    public required IReadOnlyList<string> Samples { get; init; }
}

/// <summary>Everything calibration has to say about a library.</summary>
public sealed record CalibrationReport
{
    public IReadOnlyList<FolderRoleProposal> Folders { get; init; } = [];

    public IReadOnlyList<ShapeProposal> Shapes { get; init; } = [];

    public bool IsEmpty => Folders.Count == 0 && Shapes.Count == 0;
}

/// <summary>
/// Works out what a library's own strings behave like, and proposes rules from it.
/// </summary>
/// <remarks>
/// <para>
/// A field cannot be identified from its own text. <c>Alambig Electrik - Beg er vil</c> and
/// <c>some junk - other junk</c> are the same thing to anything reading one file name. What
/// identifies a field is how it behaves across many files, plus the one vocabulary we hold, so
/// nothing here looks at a string and decides what it is.
/// </para>
/// <para>
/// Three signals: how a value moves across files (constant within a folder but varying between them
/// is an artist; distinct in every file is a title; always numeric is a track number), whether the
/// published list knows it, and whether it agrees with a tag. The last is the strongest and the only
/// one that settles a field outright; the first is the only one available at all in a library with
/// no tags worth reading.
/// </para>
/// <para>
/// The output is a proposal and never a decision. Accepting one writes a declared setting, which is
/// a bulk approval, so it is the user's to give: where the signals do not converge the honest answer
/// is to show the shape, show samples, and name nothing.
/// </para>
/// </remarks>
public static class Calibration
{
    /// <summary>Enough of a shape to be worth a rule rather than a coincidence.</summary>
    private const int LeastFilesWorthProposing = 10;

    /// <summary>How much of a shape has to agree before a position is named.</summary>
    private const double Convincing = 0.6;

    /// <summary>A track number is a number in every file or it is not a track number.</summary>
    private const double Certain = 0.9;

    /// <summary>
    /// Folders needed before "constant in a folder" means anything.
    /// </summary>
    /// <remarks>
    /// The signal is constant within a folder <em>and varying between them</em>. One folder cannot
    /// show the second half, and a value that never changes is not an artist, it is a constant.
    /// </remarks>
    private const int FoldersWorthComparing = 3;

    private const int SampleSize = 20;

    /// <summary>The separators a real library builds its names out of.</summary>
    private static readonly string[] Separators = [" - ", " -- ", "_-_"];

    /// <summary>What can be said about a library, from strings a scan has already read.</summary>
    /// <param name="files">Every file worth calibrating over, usually the ones no rule covers.</param>
    /// <param name="dances">The published list: the only field that can be checked positively.</param>
    /// <param name="declared">What the user has already stated, so nothing already answered is proposed.</param>
    public static CalibrationReport Measure(
        IReadOnlyList<CalibrationFile> files, DanceListIndex dances, DiscoverySettings declared)
    {
        return files.Count == 0
            ? new CalibrationReport()
            : new CalibrationReport
            {
                // What is in force, so a level whose role is switched off is proposed again rather
                // than counted as answered by a rule that is doing nothing.
                Folders = [.. FolderRoles(files, dances, declared.InForce())],
                Shapes = [.. Shapes(files, dances)]
            };
    }

    /// <summary>
    /// The levels that behave like something, in the order they would be declared.
    /// </summary>
    /// <remarks>
    /// Only positive answers: a level equal to the artist tag on most of its files is an artist, and
    /// a level the published list recognises is a dance. Everything else is left alone, because "not
    /// an artist" is not evidence of being anything in particular, and a level nobody can name is
    /// exactly the level a guess would ruin.
    /// </remarks>
    private static IEnumerable<FolderRoleProposal> FolderRoles(
        IReadOnlyList<CalibrationFile> files, DanceListIndex dances, DiscoverySettings declared)
    {
        var deepest = files.Max(file => file.Folders.Count);

        for (var level = 1; level <= deepest; level++)
        {
            if (declared.RoleForLevel(level) is not FolderRole.Unknown)
            {
                continue;
            }

            var atDepth = files.Where(file => file.Folders.Count >= level).ToList();
            if (atDepth.Count < LeastFilesWorthProposing)
            {
                continue;
            }

            var values = atDepth.Select(file => file.Folders[level - 1]).ToList();
            var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();

            // One value over the whole library says nothing, and a value per file says nothing
            // either: both are the same non-answer wearing different clothes.
            if (distinct <= 1 || distinct == atDepth.Count)
            {
                continue;
            }

            var withTag = atDepth.Where(file => !string.IsNullOrWhiteSpace(file.TagArtist)).ToList();
            var agreeingWithTag = withTag.Count(file =>
                Same(file.Folders[level - 1], file.TagArtist));

            if (withTag.Count >= LeastFilesWorthProposing && agreeingWithTag >= withTag.Count * Convincing)
            {
                yield return new FolderRoleProposal
                {
                    Level = level,
                    Role = FolderRole.Artist,
                    Agreeing = agreeingWithTag,
                    Considered = withTag.Count,
                    Samples = [.. values.Distinct(StringComparer.OrdinalIgnoreCase).Take(SampleSize)]
                };

                continue;
            }

            var known = values.Count(value => dances.ResolveSlug(value) is not null);
            if (known >= atDepth.Count * Convincing)
            {
                yield return new FolderRoleProposal
                {
                    Level = level,
                    Role = FolderRole.Dance,
                    Agreeing = known,
                    Considered = atDepth.Count,
                    Samples = [.. values.Distinct(StringComparer.OrdinalIgnoreCase).Take(SampleSize)]
                };
            }
        }
    }

    /// <summary>The file name shapes the library has, largest first, with what each position does.</summary>
    private static IEnumerable<ShapeProposal> Shapes(IReadOnlyList<CalibrationFile> files, DanceListIndex dances)
    {
        var proposals = new List<ShapeProposal>();

        foreach (var separator in Separators)
        {
            var byFieldCount = files
                .Select(file => (File: file, Parts: Split(file.FileName, separator)))
                .Where(split => split.Parts.Length >= 2)
                .GroupBy(split => split.Parts.Length);

            foreach (var shape in byFieldCount.Where(group => group.Count() >= LeastFilesWorthProposing))
            {
                var members = shape.ToList();
                var positions = Enumerable.Range(1, shape.Key)
                    .Select(position => Describe(position, members, dances))
                    .ToList();

                proposals.Add(new ShapeProposal
                {
                    Separator = separator,
                    Fields = shape.Key,
                    Files = members.Count,
                    Considered = files.Count,
                    Positions = positions,
                    Samples = [.. members.Take(SampleSize).Select(member => member.File.FileName)],
                    Pattern = PatternFor(positions, separator)
                });
            }
        }

        return proposals.OrderByDescending(proposal => proposal.Files);
    }

    /// <summary>What one position of a shape behaves like, and the counts that say so.</summary>
    private static PositionFinding Describe(
        int position,
        List<(CalibrationFile File, string[] Parts)> members,
        DanceListIndex dances)
    {
        var values = members.Select(member => member.Parts[position - 1]).ToList();

        var folders = members
            .GroupBy(member => string.Join('/', member.File.Folders), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();

        var constantInFolders = folders.Count(group =>
            group.Select(member => member.Parts[position - 1]).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1);

        // And varying between them. Without this, a shape that lives in one folder reports "constant
        // in 1 of 1" and every position of it reads as an artist.
        var variesBetweenFolders = folders
            .Select(group => group.First().Parts[position - 1])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 1;

        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var danceNames = values.Count(value => dances.ResolveSlug(value) is not null);
        var numeric = values.Count(value => value.Trim().Length > 0 && value.Trim().All(char.IsDigit));

        var agreesWithArtist = members.Count(member =>
            !string.IsNullOrWhiteSpace(member.File.TagArtist) && Same(member.Parts[position - 1], member.File.TagArtist));
        var agreesWithTitle = members.Count(member =>
            !string.IsNullOrWhiteSpace(member.File.TagTitle) && Same(member.Parts[position - 1], member.File.TagTitle));

        var field = Identify(
            members.Count,
            variesBetweenFolders ? folders.Count : 0,
            constantInFolders,
            distinct,
            danceNames,
            numeric,
            agreesWithArtist,
            agreesWithTitle);

        return new PositionFinding(
            position,
            field,
            constantInFolders,
            folders.Count,
            distinct,
            danceNames,
            Math.Max(agreesWithArtist, agreesWithTitle),
            members.Count);
    }

    /// <summary>
    /// What a position is, or nothing.
    /// </summary>
    /// <remarks>
    /// Ordered by how much a signal is worth. A tag agreeing settles it outright, because the tag
    /// says what the field is and the position merely happens to match. The published list is next,
    /// being the one vocabulary that can confirm rather than merely correlate. Behaviour across files
    /// is last and is all a library with no usable tags has: constant within a folder is an artist,
    /// different in every file is a title.
    /// </remarks>
    private static TrackField? Identify(
        int files,
        int folders,
        int constantInFolders,
        int distinct,
        int danceNames,
        int numeric,
        int agreesWithArtist,
        int agreesWithTitle) => true switch
        {
            // A track number is read so a pattern can say where it sits. Nothing wants one.
            _ when numeric >= files * Certain => null,
            _ when agreesWithArtist >= files * Convincing => TrackField.Artist,
            _ when agreesWithTitle >= files * Convincing => TrackField.Title,
            _ when danceNames >= files * Convincing => TrackField.Dance,
            _ when distinct >= files * Certain => TrackField.Title,
            _ when folders >= FoldersWorthComparing && constantInFolders >= folders * Convincing => TrackField.Artist,
            _ => null
        };

    /// <summary>
    /// The rule a shape would be declared as, or null when it would answer nothing.
    /// </summary>
    /// <remarks>
    /// A position nothing could name becomes <c>%i</c>: the field is there and is not one we can
    /// speak for. A number in every file becomes <c>%n</c>, which is the same statement about a
    /// field nothing wants. A shape whose positions are all one or the other is worth looking at and
    /// not worth declaring, so it comes back with no pattern at all.
    /// </remarks>
    private static string? PatternFor(IReadOnlyList<PositionFinding> positions, string separator)
    {
        if (!positions.Any(position => position.Field is not null))
        {
            return null;
        }

        // The same field cannot be claimed twice, so a second one is read as unknown rather than
        // producing a pattern that will not compile.
        var used = new HashSet<TrackField>();
        var tokens = new List<string>();

        foreach (var position in positions)
        {
            tokens.Add(position.Field is { } field && used.Add(field)
                ? field switch
                {
                    TrackField.Dance => "%d",
                    TrackField.Artist => "%a",
                    _ => "%t"
                }
                : "%i");
        }

        return string.Join(separator, tokens);
    }

    private static string[] Split(string fileName, string separator) =>
        fileName.Split(separator, StringSplitOptions.None);

    private static bool Same(string? left, string? right) =>
        StringNormalizer.Normalize(left ?? string.Empty) is { Length: > 0 } folded
        && string.Equals(folded, StringNormalizer.Normalize(right ?? string.Empty), StringComparison.Ordinal);
}
