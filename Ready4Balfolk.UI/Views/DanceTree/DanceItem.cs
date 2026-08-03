using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Editor;

namespace Ready4Balfolk.UI.Views.DanceTree;

#pragma warning disable CS8618 // _displayName is set immediately by ObservableAsPropertyHelper
public sealed partial class DanceItem : ReactiveObject, IDisposable
{
    private readonly DanceTreeContext _context;
    private readonly CompositeDisposable _disposables = [];

    public int[] ParentPath { get; }
    public int LeafIndex { get; }

    [Reactive] public partial string Name { get; set; }
    [Reactive] public partial int Weight { get; set; }
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
        new MarkedSelection.Leaf(ParentPath, LeafIndex));

    [ReactiveCommand]
    private void DeleteOrCancel()
    {
        if (IsEditing)
        {
            _context.CancelEdit(this);
        }
        else
        {
            _context.CommitTracked(store => DanceTreeAction.DeleteLeaf(store, ParentPath, LeafIndex));
        }
    }

    public void ConfirmEdit() => _context.ConfirmEdit(this);
    public void CancelEdit() => _context.CancelEdit(this);

    public DanceItem(DanceLeaf leaf, int[] parentPath, int leafIndex, DanceTreeContext context)
    {
        _context = context;
        ParentPath = parentPath;
        LeafIndex = leafIndex;
        Name = leaf.Name;
        Weight = leaf.Weight;

        var normalizedName = StringNormalizer.Normalize(leaf.Name);
        _trackCountHelper = context.TrackCounts
            .Select(counts => counts.GetValueOrDefault(normalizedName, 0))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.TrackCount);
        _trackCountHelper.DisposeWith(_disposables);

        _isMarkedHelper = context.MarkedSelection
            .Select(m => m is MarkedSelection.Leaf l
                         && l.ParentPath.SequenceEqual(parentPath)
                         && l.LeafIndex == leafIndex)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsMarked);
        _isMarkedHelper.DisposeWith(_disposables);

        _displayNameHelper = this.WhenAnyValue(x => x.Name, x => x.Weight, x => x.TrackCount)
            .Select(t => $"{t.Value1} ({t.Value3}) \u2014 weight: {t.Value2}")
            .ToProperty(this, x => x.DisplayName, initialValue: $"{leaf.Name} (0) \u2014 weight: {leaf.Weight}");
        _displayNameHelper.DisposeWith(_disposables);

        this.WhenAnyValue(x => x.IsEditing)
            .Where(editing => editing)
            .Subscribe(_ => OriginalName = Name)
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Name)
            .Skip(1)
            .Where(_ => !IsEditing)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(name => _context.CommitDirect(roots =>
                DanceTreeTransforms.RenameLeaf(roots, ParentPath, LeafIndex, name)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Weight)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(weight => _context.CommitDirect(roots =>
                DanceTreeTransforms.ReweightLeaf(roots, ParentPath, LeafIndex, weight)))
            .DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}
