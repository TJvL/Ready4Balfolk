using System;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>A tag in the rail, sized by how many dances carry it.</summary>
/// <remarks>
/// The square root rather than the count itself: a tag on sixty dances would otherwise dwarf one on
/// four to the point where the small ones are unreadable, and the rail is there to be read.
/// </remarks>
public sealed class TagChipViewModel
{
    public const double SmallestSize = 12;
    public const double LargestSize = 22;

    public TagChipViewModel(string tag, int count, int largestCount, bool isInPool, bool isReachable)
    {
        Tag = tag;
        Count = count;
        IsInPool = isInPool;

        // Dimmed rather than hidden: a tag that the search has filtered out of view still exists,
        // and a rail that reshuffles itself as you type is impossible to aim at.
        IsDimmed = !isInPool && !isReachable;

        var scale = largestCount <= 1 ? 0.5 : Math.Sqrt(count) / Math.Sqrt(largestCount);
        Size = SmallestSize + ((LargestSize - SmallestSize) * Math.Clamp(scale, 0, 1));
    }

    public string Tag { get; }

    public int Count { get; }

    public bool IsInPool { get; }

    public bool IsDimmed { get; }

    public double Size { get; }
}
