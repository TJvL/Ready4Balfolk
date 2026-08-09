using System.Globalization;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>The kinds of thing that can speak about a track, one per independent reading.</summary>
/// <remarks>
/// Independence is the whole point of this enum, because agreement between two kinds is what makes
/// an answer trustworthy. The title tag and the comment tag are one kind between them: they were
/// written by the same ripper in the same pass, so a dance appearing in both proves nothing.
/// </remarks>
public enum ClaimSourceKind
{
    /// <summary>Something written into the file's tags.</summary>
    Tag,

    /// <summary>The file's own name, read whole or through a pattern the user declared.</summary>
    FileName,

    /// <summary>The folders the file sits in, or what the rest of one turned out to be.</summary>
    Folder
}

/// <summary>Where a claim came from, precisely enough to show a person.</summary>
/// <param name="Kind">The independent reading this belongs to.</param>
/// <param name="Detail">Which part of it, for a review screen to name.</param>
/// <param name="IsDeliberate">
/// Whether somebody wrote this value as a statement about the track rather than it being read out
/// of ordinary text. A dance in brackets is deliberate; the same word inside a title is an accident
/// of language, and "Tour" is both a real dance and a French word.
/// </param>
/// <param name="IsDerived">
/// Whether it was worked out from other files rather than read off this one. A derived claim fills
/// a gap and never corroborates: agreeing with the files it was computed from is one source counted
/// twice.
/// </param>
public sealed record ClaimSource(
    ClaimSourceKind Kind, string Detail, bool IsDeliberate = false, bool IsDerived = false)
{
    public static ClaimSource Tag(string field) => new(ClaimSourceKind.Tag, field);

    public static ClaimSource FileName { get; } = new(ClaimSourceKind.FileName, "file name");

    public static ClaimSource Brackets { get; } = new(ClaimSourceKind.FileName, "brackets", IsDeliberate: true);

    /// <summary>A file name pattern the user declared, named by the pattern itself.</summary>
    public static ClaimSource Pattern(string pattern) => new(ClaimSourceKind.FileName, pattern, IsDeliberate: true);

    /// <summary>A folder level the user gave a role to, counted from 1 outermost.</summary>
    public static ClaimSource FolderLevel(int level) => new(
        ClaimSourceKind.Folder,
        string.Format(CultureInfo.InvariantCulture, "level {0}", level),
        IsDeliberate: true);

    /// <summary>What the rest of the folder resolved to. Fills a gap and never corroborates.</summary>
    public static ClaimSource FolderAgreement { get; } =
        new(ClaimSourceKind.Folder, "the rest of the folder", IsDerived: true);
}
