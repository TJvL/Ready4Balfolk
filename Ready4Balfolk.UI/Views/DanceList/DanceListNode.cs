using System.Collections.Generic;
using System.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>A row in the dance list tree.</summary>
public abstract partial class DanceListNode : ReactiveObject
{
    /// <summary>
    /// Identifies the row across rebuilds. The tree is rebuilt whole on every edit, so selection
    /// and expansion have to be restored by something more durable than an object reference.
    /// </summary>
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required int Weight { get; init; }

    public IReadOnlyList<DanceListNode> Children { get; init; } = [];

    [Reactive] public partial bool IsExpanded { get; set; }

    /// <summary>True when a random pick is scoped to this row.</summary>
    [Reactive] public partial bool IsMarked { get; set; }
}

/// <summary>A category, which is also a branch of the tree randomisation picks from.</summary>
public sealed class DanceCategoryNode : DanceListNode
{
    public required int[] Path { get; init; }

    /// <summary>How many dances sit in this category and everything under it.</summary>
    public required int DanceCount { get; init; }
}

/// <summary>A dance. Addressed by slug, because that is its identity.</summary>
public sealed class DanceNode : DanceListNode
{
    public required string Slug { get; init; }

    /// <summary>The category it currently sits in, so an edit knows where it came from.</summary>
    public required int[] CategoryPath { get; init; }

    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>The other spellings, shown after the one being displayed.</summary>
    public string OtherNames => Names.Count > 1 ? string.Join(", ", Names.Skip(1)) : string.Empty;
}
