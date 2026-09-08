using System;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>A tag in the rail, sized by how many dances carry it.</summary>
/// <remarks>
/// The square root rather than the count itself: a tag on sixty dances would otherwise dwarf one on
/// four to the point where the small ones are unreadable, and the rail is there to be read.
/// Everything but the tag itself can change while the rail is on screen, and says so: the chip is
/// kept rather than replaced, so that the keyboard standing on it stays there.
/// </remarks>
public sealed partial class TagChipViewModel : ReactiveObject, IKeepsItsPlace
{
    public const double SmallestSize = 12;
    public const double LargestSize = 22;

    public TagChipViewModel(string tag, int count, int largestCount, bool isInPool, bool isExcluded, bool isReachable)
    {
        Tag = tag;
        Show(count, largestCount, isInPool, isExcluded, isReachable);
    }

    public string Tag { get; }

    string IKeepsItsPlace.Key => Tag;

    [Reactive] public partial int Count { get; private set; }

    [Reactive] public partial bool IsInPool { get; private set; }

    /// <summary>Never drawn: the tag's third state, after "in the pool".</summary>
    [Reactive] public partial bool IsExcluded { get; private set; }

    [Reactive] public partial bool IsDimmed { get; private set; }

    [Reactive] public partial double Size { get; private set; }

    /// <summary>Takes what the rail knows now, in place, so the control drawn from it stays.</summary>
    public void Show(int count, int largestCount, bool isInPool, bool isExcluded, bool isReachable)
    {
        Count = count;
        IsInPool = isInPool;
        IsExcluded = isExcluded;

        // Dimmed rather than hidden: a tag that the search has filtered out of view still exists,
        // and a rail that reshuffles itself as you type is impossible to aim at.
        IsDimmed = !isInPool && !isExcluded && !isReachable;

        var scale = largestCount <= 1 ? 0.5 : Math.Sqrt(count) / Math.Sqrt(largestCount);
        Size = SmallestSize + ((LargestSize - SmallestSize) * Math.Clamp(scale, 0, 1));
    }
}
