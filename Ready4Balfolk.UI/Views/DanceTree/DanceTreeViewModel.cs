using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceTree;

#pragma warning disable CS8618 // ObservableAsProperty fields set by helpers in constructor
public sealed partial class DanceTreeViewModel : ReactiveObject, IDisposable
{
    private readonly IDanceTreeStore _danceTreeStore;
    private readonly IEditorHistoryService _editorHistoryService;
    private readonly ISettingsStore _settingsStore;
    private readonly IRandomTrackService _randomTrackService;
    private readonly IQueueService _queueService;
    private readonly INotificationService _notificationService;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];
    private readonly IObservable<IReadOnlyDictionary<string, int>> _trackCounts;
    private HashSet<string>? _collapsedBranches;
    private int _pendingCommits;

    private sealed record PendingSelection(int[] Path, int? LeafIndex, bool EnterEditMode, bool IsNewlyAdded);

    private PendingSelection? _pendingSelection;
    private object? _previousSelectedItem;

    [Reactive] public partial IReadOnlyList<DanceCategoryNode> DanceTreeDisplayRoot { get; set; }
    [Reactive] public partial object? SelectedTreeItem { get; set; }
    [Reactive] public partial MarkedSelection Marked { get; set; }

    [ObservableAsProperty] public partial bool IsLoading { get; }
    [ObservableAsProperty] public partial bool HasEntries { get; }
    [ObservableAsProperty] public partial string? UndoTooltip { get; }
    [ObservableAsProperty] public partial string? RedoTooltip { get; }

    private IObservable<bool> CanUndo => _editorHistoryService.CanUndo;
    private IObservable<bool> CanRedo => _editorHistoryService.CanRedo;

    [ReactiveCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRedoAsync(_editorHistoryService.UndoAsync);

    [ReactiveCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => UndoRedoAsync(_editorHistoryService.RedoAsync);

    public IObservable<MarkedSelection> WhenMarkedChanged { get; }

    public void QuickRandomPick(object node)
    {
        RandomSelectionScope scope = node switch
        {
            DanceCategoryNode { IsRoot: true } => new RandomSelectionScope.EntireTree(),
            DanceCategoryNode branch => new RandomSelectionScope.Subtree(branch.Path),
            DanceItem leaf => new RandomSelectionScope.SingleDance(leaf.ParentPath, leaf.LeafIndex),
            _ => new RandomSelectionScope.EntireTree()
        };

        var track = _randomTrackService.PickRandomTrack(
            scope, _settingsStore.Current.AllowDuplicateTracksInQueue);

        if (track is not null)
        {
            var result = _queueService.Enqueue(new TrackQueueItem(track, RandomlyAdded: true));
            if (!result.Allowed)
            {
                _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
            }
        }
        else
        {
            _notificationService.Show(UiStrings.DanceTree_NoTracksAvailable,
                NotificationSeverity.Warning);
        }
    }

    public DanceTreeViewModel(
        IDanceTreeStore danceTreeStore,
        IEditorHistoryService editorHistoryService,
        ITrackStore trackStore,
        ISettingsStore settingsStore,
        IRandomTrackService randomTrackService,
        IQueueService queueService,
        INotificationService notificationService,
        ILoggerService loggerService)
    {
        _danceTreeStore = danceTreeStore;
        _editorHistoryService = editorHistoryService;
        _settingsStore = settingsStore;
        _randomTrackService = randomTrackService;
        _queueService = queueService;
        _notificationService = notificationService;
        _loggerService = loggerService;
        DanceTreeDisplayRoot = new List<DanceCategoryNode>();
        Marked = new MarkedSelection.Root();

        WhenMarkedChanged = this.WhenAnyValue(x => x.Marked);

        _isLoadingHelper = danceTreeStore.IsLoading
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        _trackCounts = trackStore.Connect()
            .ToCollection()
            .Select(IReadOnlyDictionary<string, int> (tracks) => tracks
                .GroupBy(t => StringNormalizer.Normalize(t.Dance))
                .ToDictionary(g => g.Key, g => g.Count()))
            .Replay(1)
            .RefCount();

        _hasEntriesHelper = this.WhenAnyValue(x => x.DanceTreeDisplayRoot)
            .Select(roots => roots.Count > 0 && roots[0].Items.Count > 0)
            .ToProperty(this, x => x.HasEntries);
        _hasEntriesHelper.DisposeWith(_disposables);

        _undoTooltipHelper = editorHistoryService.UndoDescription
            .Select(desc => desc is not null ? string.Format(CultureInfo.CurrentCulture, UiStrings.DanceTreeToolbar_UndoFormat, desc) : UiStrings.DanceTreeToolbar_UndoDefault)
            .ToProperty(this, x => x.UndoTooltip);
        _undoTooltipHelper.DisposeWith(_disposables);

        _redoTooltipHelper = editorHistoryService.RedoDescription
            .Select(desc => desc is not null ? string.Format(CultureInfo.CurrentCulture, UiStrings.DanceTreeToolbar_RedoFormat, desc) : UiStrings.DanceTreeToolbar_RedoDefault)
            .ToProperty(this, x => x.RedoTooltip);
        _redoTooltipHelper.DisposeWith(_disposables);

        // Selection-change guard: cancel active edit on previous item
        this.WhenAnyValue(x => x.SelectedTreeItem)
            .Subscribe(current =>
            {
                var prev = _previousSelectedItem;
                _previousSelectedItem = current;

                switch (prev)
                {
                    case DanceCategoryNode { IsEditing: true } branch:
                        CancelEditInline(branch.IsNewlyAdded, () =>
                        {
                            branch.Name = branch.OriginalName!;
                            branch.IsEditing = false;
                            branch.IsNewlyAdded = false;
                        });
                        break;
                    case DanceItem { IsEditing: true } leaf:
                        CancelEditInline(leaf.IsNewlyAdded, () =>
                        {
                            leaf.Name = leaf.OriginalName!;
                            leaf.IsEditing = false;
                            leaf.IsNewlyAdded = false;
                        });
                        break;
                    default:
                        break;
                }
            })
            .DisposeWith(_disposables);

        danceTreeStore.Observe()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => _pendingCommits == 0)
            .Subscribe(RebuildDisplayTree)
            .DisposeWith(_disposables);
    }

    private void CancelEditInline(bool isNewlyAdded, Action revertAction)
    {
        if (isNewlyAdded)
        {
            UndoRedoAsync(_editorHistoryService.UndoAsync);
        }
        else
        {
            revertAction();
        }
    }

    private void RebuildDisplayTree(IReadOnlyList<DanceBranch> branches)
    {
        if (_collapsedBranches is not null)
        {
            CaptureExpansionState();
        }
        else if (branches.Count > 0)
        {
            _collapsedBranches = [.. _settingsStore.Current.CollapsedBranches];
        }

        foreach (var node in DanceTreeDisplayRoot)
        {
            node.Dispose();
        }

        // Validate marked selection still exists; reset to Root if invalid
        if (!IsMarkedPathValid(branches, Marked))
        {
            Marked = new MarkedSelection.Root();
        }

        var context = new DanceTreeContext(
            CommitDirect, CommitTracked, _trackCounts, WhenMarkedChanged, m => Marked = m,
            HandleRequestAddBranch, HandleRequestAddLeaf, HandleConfirmEdit, HandleCancelEdit,
            _collapsedBranches ?? []);
        var root = new DanceCategoryNode(UiStrings.DanceTree_RootName, branches, context);
        DanceTreeDisplayRoot = new List<DanceCategoryNode>
        {
            root
        };

        if (_pendingSelection is { } pending)
        {
            _pendingSelection = null;
            var found = root.FindNode(pending.Path, pending.LeafIndex);
            if (found is not null)
            {
                SelectedTreeItem = found;
                if (pending.EnterEditMode)
                {
                    switch (found)
                    {
                        case DanceCategoryNode branch:
                            branch.IsEditing = true;
                            branch.IsNewlyAdded = pending.IsNewlyAdded;
                            break;
                        case DanceItem leaf:
                            leaf.IsEditing = true;
                            leaf.IsNewlyAdded = pending.IsNewlyAdded;
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                SelectedTreeItem = root;
            }
        }
        else
        {
            SelectedTreeItem = root;
        }
    }

    private void HandleRequestAddBranch(DanceCategoryNode parent)
    {
        var roots = _danceTreeStore.Current;
        var parentPath = parent.Path;

        int newBranchIndex;
        if (parentPath.Length == 0)
        {
            newBranchIndex = roots.Count;
        }
        else
        {
            var branch = ResolveBranch(roots, parentPath);
            newBranchIndex = branch?.Branches.Count() ?? 0;
        }

        _pendingSelection = new PendingSelection([.. parentPath, newBranchIndex], null, true, true);
        CommitTracked(store => DanceTreeAction.AddBranch(store, parentPath));
    }

    private void HandleRequestAddLeaf(DanceCategoryNode parent)
    {
        var roots = _danceTreeStore.Current;
        var parentPath = parent.Path;

        if (parentPath.Length == 0)
        {
            return;
        }

        var branch = ResolveBranch(roots, parentPath);
        var newLeafIndex = branch?.Leafs.Count() ?? 0;

        _pendingSelection = new PendingSelection(parentPath, newLeafIndex, true, true);
        CommitTracked(store => DanceTreeAction.AddLeaf(store, parentPath));
    }

    private void HandleConfirmEdit(object node)
    {
        switch (node)
        {
            case DanceCategoryNode branch:
                ConfirmBranchEdit(branch);
                break;
            case DanceItem leaf:
                ConfirmLeafEdit(leaf);
                break;
            default:
                break;
        }
    }

    private void ConfirmBranchEdit(DanceCategoryNode branch)
    {
        var name = branch.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _notificationService.Show(UiStrings.DanceTree_NameEmpty, NotificationSeverity.Warning);
            return;
        }

        var roots = _danceTreeStore.Current;

        if (!DanceTreeTransforms.IsBranchNameUnique(roots, name, excludePath: branch.Path))
        {
            _notificationService.Show(
                string.Format(CultureInfo.CurrentCulture, UiStrings.DanceTree_CategoryNameExists, name), NotificationSeverity.Warning);
            return;
        }

        if (!DanceTreeTransforms.IsLeafNameUnique(roots, name))
        {
            _notificationService.Show(
                string.Format(CultureInfo.CurrentCulture, UiStrings.DanceTree_DanceNameExists, name), NotificationSeverity.Warning);
            return;
        }

        branch.IsEditing = false;
        branch.IsNewlyAdded = false;

        _pendingSelection = new PendingSelection(branch.Path, null, false, false);
        CommitDirectAndRebuild(r => DanceTreeTransforms.RenameBranch(r, branch.Path, name));
    }

    private void ConfirmLeafEdit(DanceItem leaf)
    {
        var name = leaf.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _notificationService.Show(UiStrings.DanceTree_NameEmpty, NotificationSeverity.Warning);
            return;
        }

        var roots = _danceTreeStore.Current;

        if (!DanceTreeTransforms.IsLeafNameUnique(roots, name,
                excludeParentPath: leaf.ParentPath, excludeLeafIndex: leaf.LeafIndex))
        {
            _notificationService.Show(
                string.Format(CultureInfo.CurrentCulture, UiStrings.DanceTree_DanceNameExists, name), NotificationSeverity.Warning);
            return;
        }

        if (!DanceTreeTransforms.IsBranchNameUnique(roots, name))
        {
            _notificationService.Show(
                string.Format(CultureInfo.CurrentCulture, UiStrings.DanceTree_CategoryNameExists, name), NotificationSeverity.Warning);
            return;
        }

        leaf.IsEditing = false;
        leaf.IsNewlyAdded = false;

        _pendingSelection = new PendingSelection(leaf.ParentPath, leaf.LeafIndex, false, false);
        CommitDirectAndRebuild(r => DanceTreeTransforms.RenameLeaf(r, leaf.ParentPath, leaf.LeafIndex, name));
    }

    private void HandleCancelEdit(object node)
    {
        switch (node)
        {
            case DanceCategoryNode { IsNewlyAdded: true }:
                UndoRedoAsync(_editorHistoryService.UndoAsync);
                break;
            case DanceCategoryNode branch:
                branch.Name = branch.OriginalName!;
                branch.IsEditing = false;
                break;
            case DanceItem { IsNewlyAdded: true }:
                UndoRedoAsync(_editorHistoryService.UndoAsync);
                break;
            case DanceItem leaf:
                leaf.Name = leaf.OriginalName!;
                leaf.IsEditing = false;
                break;
            default:
                break;
        }
    }

    private static DanceBranch? ResolveBranch(IReadOnlyList<DanceBranch> roots, int[] path)
    {
        var level = roots;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= level.Count)
            {
                return null;
            }

            if (i == path.Length - 1)
            {
                return level[path[i]];
            }

            level = level[path[i]].Branches.ToList();
        }

        return null;
    }

    private static bool IsMarkedPathValid(IReadOnlyList<DanceBranch> roots, MarkedSelection marked)
    {
        return marked switch
        {
            MarkedSelection.Root => true,
            MarkedSelection.Branch b => ResolveBranchExists(roots, b.Path),
            MarkedSelection.Leaf l => ResolveLeafExists(roots, l.ParentPath, l.LeafIndex),
            _ => false
        };
    }

    private static bool ResolveBranchExists(IReadOnlyList<DanceBranch> roots, int[] path)
    {
        var level = roots;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= level.Count)
            {
                return false;
            }

            if (i < path.Length - 1)
            {
                level = level[path[i]].Branches.ToList();
            }
        }

        return true;
    }

    private static bool ResolveLeafExists(IReadOnlyList<DanceBranch> roots, int[] parentPath,
        int leafIndex)
    {
        var level = roots;
        for (var i = 0; i < parentPath.Length; i++)
        {
            if (parentPath[i] < 0 || parentPath[i] >= level.Count)
            {
                return false;
            }

            if (i == parentPath.Length - 1)
            {
                var leafs = level[parentPath[i]].Leafs.ToList();
                return leafIndex >= 0 && leafIndex < leafs.Count;
            }

            level = level[parentPath[i]].Branches.ToList();
        }

        return false;
    }

    private async void CommitDirect(
        Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>> transform)
    {
        _pendingCommits++;
        try
        {
            await _danceTreeStore.UpdateAsync(transform);
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to save dance tree changes", ex);
            return;
        }
        finally
        {
            _pendingCommits--;
        }
    }

    private async void CommitDirectAndRebuild(
        Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>> transform)
    {
        _pendingCommits++;
        try
        {
            await _danceTreeStore.UpdateAsync(transform);
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to save dance tree changes", ex);
            return;
        }
        finally
        {
            _pendingCommits--;
        }

        RebuildDisplayTree(_danceTreeStore.Current);
    }

    private async void CommitTracked(Func<IDanceTreeStore, DanceTreeAction> actionFactory)
    {
        var action = actionFactory(_danceTreeStore);
        _pendingCommits++;
        try
        {
            await _editorHistoryService.DoActionAsync(action);
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to save dance tree changes", ex);
            return;
        }
        finally
        {
            _pendingCommits--;
        }

        RebuildDisplayTree(_danceTreeStore.Current);
    }

    private async void UndoRedoAsync(Func<Task> op)
    {
        _pendingCommits++;
        try
        {
            await op();
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to undo/redo dance tree changes", ex);
            return;
        }
        finally
        {
            _pendingCommits--;
        }

        RebuildDisplayTree(_danceTreeStore.Current);
    }

    public async Task ImportAsync(FileInfo file)
    {
        await _danceTreeStore.ImportAsync(file);
        _editorHistoryService.Clear();
        RebuildDisplayTree(_danceTreeStore.Current);
    }

    public async Task ExportAsync(FileInfo file) => await _danceTreeStore.ExportAsync(file);

    public HashSet<string> GetCollapsedBranches()
    {
        var collapsed = new HashSet<string>();
        foreach (var root in DanceTreeDisplayRoot)
        {
            CollectCollapsedRecursive(root, collapsed);
        }

        return collapsed;
    }

    private void CaptureExpansionState() => _collapsedBranches = GetCollapsedBranches();

    private static void CollectCollapsedRecursive(DanceCategoryNode node, HashSet<string> collapsed)
    {
        if (!node.IsRoot && !node.IsExpanded)
        {
            collapsed.Add(node.Name);
        }

        foreach (var child in node.Items.OfType<DanceCategoryNode>())
        {
            CollectCollapsedRecursive(child, collapsed);
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        foreach (var node in DanceTreeDisplayRoot)
        {
            node.Dispose();
        }
    }
}
