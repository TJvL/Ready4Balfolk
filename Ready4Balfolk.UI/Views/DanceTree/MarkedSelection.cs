namespace Ready4Balfolk.UI.Views.DanceTree;

public abstract record MarkedSelection
{
    public sealed record Root : MarkedSelection;

    public sealed record Branch(int[] Path) : MarkedSelection;

    public sealed record Leaf(int[] ParentPath, int LeafIndex) : MarkedSelection;
}
