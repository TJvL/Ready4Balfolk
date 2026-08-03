using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Editor;

namespace Ready4Balfolk.UI.Views.DanceTree;

#pragma warning disable CS8618 // _displayName is set immediately by ObservableAsPropertyHelper
public sealed partial class DanceCategoryNode : ReactiveObject, IDisposable
{
    private readonly DanceTreeContext _context;
    private readonly CompositeDisposable _disposables = [];

    public int[] Path { get; }

    public bool IsRoot => Path.Length == 0;
    public IReadOnlyList<object> Items { get; }

    [Reactive] public partial string Name { get; set; }
    [Reactive] public partial int Weight { get; set; }
    [Reactive] public partial bool IsExpanded { get; set; }
    [Reactive] public partial bool IsEditing { get; set; }
    [Reactive] public partial bool IsNewlyAdded { get; set; }

    public string? OriginalName { get; private set; }

    [ObservableAsProperty] public partial string DisplayName { get; }

    // ReSharper disable once MemberCanBePrivate.Global
    [ObservableAsProperty] public partial int TrackCount { get; }
    [ObservableAsProperty] public partial bool IsMarked { get; }

    [ReactiveCommand]
    private void ToggleEditing()
    {
        if (IsEditing)
        {
            _context.ConfirmEdit(this);
        }
        else
        {
            IsEditing = true;
        }
    }

    [ReactiveCommand]
    private void MarkAsRoot() => _context.SetMarked(
        IsRoot ? new MarkedSelection.Root() : new MarkedSelection.Branch(Path));

    private IObservable<bool> CanDelete => Observable.Return(!IsRoot);

    [ReactiveCommand]
    private void AddCategory() => _context.RequestAddBranch(this);

    [ReactiveCommand]
    private void AddDance() => _context.RequestAddLeaf(this);

    [ReactiveCommand(CanExecute = nameof(CanDelete))]
    private void DeleteOrCancel()
    {
        if (IsEditing)
        {
            _context.CancelEdit(this);
        }
        else
        {
            _context.CommitTracked(store => DanceTreeAction.DeleteBranch(store, Path));
        }
    }

    public void ConfirmEdit() => _context.ConfirmEdit(this);
    public void CancelEdit() => _context.CancelEdit(this);

    /// <summary>Regular branch constructor.</summary>
    private DanceCategoryNode(DanceBranch branch, int[] path, DanceTreeContext context)
    {
        _context = context;
        Path = path;
        Name = branch.Name;
        Weight = branch.Weight;
        IsExpanded = !context.CollapsedBranches.Contains(branch.Name);

        var branches = branch.Branches.ToList();
        var leafs = branch.Leafs.ToList();
        var items = branches
            .Select((t, i) => new DanceCategoryNode(t, [.. path, i], context))
            .Cast<object>().ToList();
        items.AddRange(leafs.Select((t, i) => new DanceItem(t, path, i, context)));

        Items = items;

        var leafNames = CollectDescendantLeafNames();
        _trackCountHelper = BuildTrackCountProperty(context, leafNames);
        _isMarkedHelper = BuildIsMarkedProperty(context);
        _displayNameHelper = BuildDisplayNameProperty();
        WireEditingCapture();
        WireCommitSubscriptions();
    }

    /// <summary>Virtual root constructor.</summary>
    public DanceCategoryNode(string rootName, IReadOnlyList<DanceBranch> topLevelBranches,
        DanceTreeContext context)
    {
        _context = context;
        Path = [];
        Name = rootName;
        Weight = 0;
        IsExpanded = true;

        var items = new List<object>();
        for (var i = 0; i < topLevelBranches.Count; i++)
        {
            items.Add(new DanceCategoryNode(topLevelBranches[i], [i], context));
        }

        Items = items;

        var leafNames = CollectDescendantLeafNames();
        _trackCountHelper = BuildTrackCountProperty(context, leafNames);
        _isMarkedHelper = BuildIsMarkedProperty(context);
        _displayNameHelper = BuildDisplayNameProperty();
        WireEditingCapture();
    }

    /// <summary>Navigates the tree to find a node at the given path/leafIndex.</summary>
    public object? FindNode(int[] path, int? leafIndex)
    {
        var current = this;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= current.Items.Count)
            {
                return null;
            }

            if (current.Items[path[i]] is not DanceCategoryNode branch)
            {
                return null;
            }

            current = branch;
        }

        if (leafIndex is null)
        {
            return current;
        }

        var leaves = current.Items.OfType<DanceItem>().ToList();
        return leafIndex.Value < leaves.Count ? leaves[leafIndex.Value] : null;
    }

    private void WireEditingCapture()
    {
        this.WhenAnyValue(x => x.IsEditing)
            .Where(editing => editing)
            .Subscribe(_ => OriginalName = Name)
            .DisposeWith(_disposables);
    }

    private ObservableAsPropertyHelper<int> BuildTrackCountProperty(
        DanceTreeContext context, List<string> leafNames)
    {
        var helper = context.TrackCounts
            .Select(counts =>
            {
                var total = 0;
                foreach (var name in leafNames)
                {
                    if (counts.TryGetValue(name, out var count))
                    {
                        total += count;
                    }
                }

                return total;
            })
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.TrackCount);
        helper.DisposeWith(_disposables);
        return helper;
    }

    private ObservableAsPropertyHelper<bool> BuildIsMarkedProperty(DanceTreeContext context)
    {
        var helper = context.MarkedSelection
            .Select(m => m switch
            {
                MarkedSelection.Root => IsRoot,
                MarkedSelection.Branch b => b.Path.SequenceEqual(Path),
                _ => false
            })
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsMarked);
        helper.DisposeWith(_disposables);
        return helper;
    }

    private List<string> CollectDescendantLeafNames()
    {
        var names = new List<string>();
        CollectLeafNamesRecursive(Items, names);
        return names;
    }

    private static void CollectLeafNamesRecursive(IEnumerable<object> items, List<string> names)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case DanceItem leaf:
                    names.Add(StringNormalizer.Normalize(leaf.Name));
                    break;
                case DanceCategoryNode branch:
                    CollectLeafNamesRecursive(branch.Items, names);
                    break;
                default:
                    break;
            }
        }
    }

    private ObservableAsPropertyHelper<string> BuildDisplayNameProperty()
    {
        var helper = this.WhenAnyValue(x => x.Name, x => x.Weight, x => x.TrackCount)
            .Select(t => IsRoot
                ? t.Item1
                : $"{t.Item1} ({t.Item3}) \u2014 weight: {t.Item2}")
            .ToProperty(this, x => x.DisplayName, initialValue: Name);
        helper.DisposeWith(_disposables);
        return helper;
    }

    private void WireCommitSubscriptions()
    {
        this.WhenAnyValue(x => x.Name)
            .Skip(1)
            .Where(_ => !IsEditing)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(name => _context.CommitDirect(roots => DanceTreeTransforms.RenameBranch(roots, Path, name)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Weight)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(weight =>
                _context.CommitDirect(roots => DanceTreeTransforms.ReweightBranch(roots, Path, weight)))
            .DisposeWith(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        foreach (var item in Items)
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
