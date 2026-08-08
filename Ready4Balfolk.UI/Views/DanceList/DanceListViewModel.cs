using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
// The view namespace is also called DanceList, so the model needs a name of its own here.
using DanceListModel = Ready4Balfolk.Domain.Models.Dances.DanceList;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>The dance list editor: categories, dances, the names each dance goes by, and weights.</summary>
/// <remarks>
/// One screen, used in two places: as a step of the setup wizard, so an imported list can be
/// corrected straight away, and as the ordinary editor afterwards. It is the same view model in
/// both, because there is nothing about editing the list that setup does differently.
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class DanceListViewModel : ReactiveObject, IDisposable
{
    private readonly IDanceListStore _store;
    private readonly IEditorHistoryService _editorHistory;
    private readonly INotificationService _notifications;
    private readonly IConfirmationService _confirmations;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];
    private readonly HashSet<string> _expandedKeys = new(StringComparer.Ordinal);
    // Rebuilt with the tree: every node is a new object, so the previous batch of subscriptions is
    // watching rows nobody can see any more.
    private readonly CompositeDisposable _nodeSubscriptions = [];
    private string? _selectedKey;
    private string? _markedKey;
    private bool _restoringSelection;

    [Reactive] public partial IReadOnlyList<DanceListNode> Nodes { get; private set; }
    [Reactive] public partial DanceListNode? SelectedNode { get; set; }

    /// <summary>The spellings of the selected dance, empty when a category is selected.</summary>
    [Reactive] public partial IReadOnlyList<DanceNameRow> Names { get; private set; }

    /// <summary>The name being edited in the details pane, committed on Enter or on the button.</summary>
    [Reactive] public partial string EditedCategoryName { get; set; }

    [Reactive] public partial int EditedWeight { get; set; }

    [Reactive] public partial string NewNameText { get; set; }

    [Reactive] public partial string NewDanceName { get; set; }

    [ObservableAsProperty] public partial bool IsLoading { get; }
    [ObservableAsProperty] public partial bool IsCategorySelected { get; }
    [ObservableAsProperty] public partial bool IsDanceSelected { get; }
    [ObservableAsProperty] public partial bool HasSelection { get; }
    [ObservableAsProperty] public partial string SelectionHeader { get; }
    [ObservableAsProperty] public partial string SummaryText { get; }
    /// <summary>What a random pick is currently scoped to, for the toolbar to say out loud.</summary>
    [Reactive] public partial string MarkedDescription { get; private set; }

    [ObservableAsProperty] public partial string? UndoTooltip { get; }
    [ObservableAsProperty] public partial string? RedoTooltip { get; }

    public DanceListViewModel(
        IDanceListStore store,
        IEditorHistoryService editorHistory,
        INotificationService notifications,
        IConfirmationService confirmations,
        ILoggerService loggerService)
    {
        _store = store;
        _editorHistory = editorHistory;
        _notifications = notifications;
        _confirmations = confirmations;
        _loggerService = loggerService;

        Nodes = [];
        Names = [];
        EditedCategoryName = string.Empty;
        NewNameText = string.Empty;
        NewDanceName = string.Empty;
        EditedWeight = 1;
        MarkedDescription = UiStrings.DanceList_MarkedWholeList;

        _isLoadingHelper = store.IsLoading
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        _isCategorySelectedHelper = this.WhenAnyValue(x => x.SelectedNode)
            .Select(node => node is DanceCategoryNode)
            .ToProperty(this, x => x.IsCategorySelected);
        _isCategorySelectedHelper.DisposeWith(_disposables);

        _isDanceSelectedHelper = this.WhenAnyValue(x => x.SelectedNode)
            .Select(node => node is DanceNode)
            .ToProperty(this, x => x.IsDanceSelected);
        _isDanceSelectedHelper.DisposeWith(_disposables);

        _hasSelectionHelper = this.WhenAnyValue(x => x.SelectedNode)
            .Select(node => node is not null)
            .ToProperty(this, x => x.HasSelection);
        _hasSelectionHelper.DisposeWith(_disposables);

        _selectionHeaderHelper = this.WhenAnyValue(x => x.SelectedNode)
            .Select(node => node?.Label ?? UiStrings.DanceList_NothingSelected)
            .ToProperty(this, x => x.SelectionHeader);
        _selectionHeaderHelper.DisposeWith(_disposables);

        _summaryTextHelper = store.Observe()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(list => string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.DanceList_SummaryFormat,
                list.AllDances.Count(),
                CountCategories(list.Categories)))
            .ToProperty(this, x => x.SummaryText);
        _summaryTextHelper.DisposeWith(_disposables);

        _undoTooltipHelper = editorHistory.UndoDescription
            .Select(description => description is not null
                ? string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_UndoFormat, description)
                : UiStrings.DanceList_UndoDefault)
            .ToProperty(this, x => x.UndoTooltip);
        _undoTooltipHelper.DisposeWith(_disposables);

        _redoTooltipHelper = editorHistory.RedoDescription
            .Select(description => description is not null
                ? string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_RedoFormat, description)
                : UiStrings.DanceList_RedoDefault)
            .ToProperty(this, x => x.RedoTooltip);
        _redoTooltipHelper.DisposeWith(_disposables);

        store.Observe()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(Rebuild)
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedNode)
            .Where(_ => !_restoringSelection)
            .Subscribe(OnSelectionChanged)
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// Where a random pick may look. Resolved from the tree every time rather than stored, so a
    /// marked category that has since been deleted quietly falls back to the whole list instead of
    /// scoping the pick to something that is not there.
    /// </summary>
    public RandomSelectionScope CurrentScope =>
        (_markedKey is null ? null : FindByKey(Nodes, _markedKey)) switch
        {
            DanceCategoryNode category => new RandomSelectionScope.Category(category.Path),
            DanceNode dance => new RandomSelectionScope.SingleDance(dance.Slug),
            _ => new RandomSelectionScope.EntireList()
        };

    private IObservable<bool> CanUndo => _editorHistory.CanUndo;
    private IObservable<bool> CanRedo => _editorHistory.CanRedo;
    private IObservable<bool> WhenCategorySelected => this.WhenAnyValue(x => x.IsCategorySelected);
    private IObservable<bool> WhenDanceSelected => this.WhenAnyValue(x => x.IsDanceSelected);
    private IObservable<bool> WhenAnythingSelected => this.WhenAnyValue(x => x.HasSelection);

    [ReactiveCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => RunHistoryAsync(_editorHistory.UndoAsync);

    [ReactiveCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => RunHistoryAsync(_editorHistory.RedoAsync);

    /// <summary>Scopes random picks to a row, or back to the whole list when it is already marked.</summary>
    [ReactiveCommand]
    private void ToggleMark(DanceListNode node)
    {
        _markedKey = string.Equals(_markedKey, node.Key, StringComparison.Ordinal) ? null : node.Key;
        ApplyMarks();
    }

    [ReactiveCommand]
    private void MarkWholeList()
    {
        _markedKey = null;
        ApplyMarks();
    }

    [ReactiveCommand]
    private void AddRootCategory() => Commit(DanceListAction.AddCategory(_store, []));

    [ReactiveCommand(CanExecute = nameof(WhenCategorySelected))]
    private void AddSubCategory()
    {
        if (SelectedNode is DanceCategoryNode category)
        {
            _expandedKeys.Add(category.Key);
            Commit(DanceListAction.AddCategory(_store, category.Path));
        }
    }

    [ReactiveCommand(CanExecute = nameof(WhenCategorySelected))]
    private void AddDance()
    {
        if (SelectedNode is not DanceCategoryNode category || string.IsNullOrWhiteSpace(NewDanceName))
        {
            return;
        }

        _expandedKeys.Add(category.Key);
        Commit(DanceListAction.AddDance(_store, category.Path, NewDanceName),
            onSuccess: () => NewDanceName = string.Empty);
    }

    [ReactiveCommand(CanExecute = nameof(WhenCategorySelected))]
    private void RenameCategory()
    {
        if (SelectedNode is DanceCategoryNode category && !string.IsNullOrWhiteSpace(EditedCategoryName))
        {
            Commit(DanceListAction.RenameCategory(_store, category.Path, EditedCategoryName));
        }
    }

    [ReactiveCommand(CanExecute = nameof(WhenAnythingSelected))]
    private void ApplyWeight()
    {
        switch (SelectedNode)
        {
            case DanceCategoryNode category:
                Commit(DanceListAction.ReweightCategory(_store, category.Path, EditedWeight));
                break;
            case DanceNode dance:
                Commit(DanceListAction.ReweightDance(_store, dance.Slug, EditedWeight));
                break;
            default:
                break;
        }
    }

    [ReactiveCommand(CanExecute = nameof(WhenDanceSelected))]
    private void AddName()
    {
        if (SelectedNode is DanceNode dance && !string.IsNullOrWhiteSpace(NewNameText))
        {
            Commit(DanceListAction.AddName(_store, dance.Slug, NewNameText),
                onSuccess: () => NewNameText = string.Empty);
        }
    }

    [ReactiveCommand]
    private void RemoveName(DanceNameRow row)
    {
        if (SelectedNode is DanceNode dance)
        {
            Commit(DanceListAction.RemoveName(_store, dance.Slug, row.Index));
        }
    }

    /// <summary>Moves a spelling to the front, which is how the displayed one is chosen.</summary>
    [ReactiveCommand]
    private void UseName(DanceNameRow row)
    {
        if (SelectedNode is DanceNode dance && row.Index > 0)
        {
            Commit(DanceListAction.MoveName(_store, dance.Slug, row.Index, 0));
        }
    }

    [ReactiveCommand(CanExecute = nameof(WhenAnythingSelected))]
    private void DeleteSelected() => DeleteSelectedAsync().SafeFireAndForget(
        exception => _loggerService.ErrorAsync("Failed to delete from the dance list", exception));

    public async Task ImportAsync(FileInfo fileInfo)
    {
        await _store.ImportAsync(fileInfo);
        _editorHistory.Clear();
    }

    public Task ExportAsync(FileInfo fileInfo) => _store.ExportAsync(fileInfo);

    public void Dispose()
    {
        _nodeSubscriptions.Dispose();
        _disposables.Dispose();
    }

    private async Task DeleteSelectedAsync()
    {
        switch (SelectedNode)
        {
            case DanceCategoryNode category:
            {
                // Deleting a category takes its dances with it, so say how many rather than
                // letting the user find out afterwards.
                var message = category.DanceCount == 0
                    ? string.Format(CultureInfo.CurrentCulture,
                        UiStrings.DanceList_DeleteEmptyCategoryMessage, category.Label)
                    : string.Format(CultureInfo.CurrentCulture,
                        UiStrings.DanceList_DeleteCategoryMessage, category.Label, category.DanceCount);

                if (await _confirmations.ConfirmAsync(UiStrings.DanceList_DeleteTitle, message,
                        UiStrings.DanceList_DeleteConfirm, UiStrings.DanceList_DeleteCancel))
                {
                    Commit(DanceListAction.DeleteCategory(_store, category.Path));
                }

                break;
            }

            case DanceNode dance:
            {
                var message = string.Format(CultureInfo.CurrentCulture,
                    UiStrings.DanceList_DeleteDanceMessage, dance.Label);
                if (await _confirmations.ConfirmAsync(UiStrings.DanceList_DeleteTitle, message,
                        UiStrings.DanceList_DeleteConfirm, UiStrings.DanceList_DeleteCancel))
                {
                    Commit(DanceListAction.DeleteDance(_store, dance.Slug));
                }

                break;
            }

            default:
                break;
        }
    }

    /// <summary>
    /// Runs an edit and reports a refusal as a message. Deliberately <c>async void</c>: the store
    /// writes the file inside the update, and blocking the UI thread on that deadlocks it.
    /// </summary>
    private async void Commit(DanceListAction action, Action? onSuccess = null)
    {
        EditorActionResult result;
        try
        {
            result = await _editorHistory.DoActionAsync(action);
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Failed to change the dance list", exception);
            return;
        }

        if (result.Success)
        {
            onSuccess?.Invoke();
            return;
        }

        if (result.ErrorMessage is not null)
        {
            _notifications.Show(result.ErrorMessage, NotificationSeverity.Warning);
        }
    }

    private void RunHistoryAsync(Func<Task> operation) =>
        operation().SafeFireAndForget(
            exception => _loggerService.ErrorAsync("Dance list undo or redo failed", exception));

    private void OnSelectionChanged(DanceListNode? node)
    {
        _selectedKey = node?.Key;

        switch (node)
        {
            case DanceCategoryNode category:
                EditedCategoryName = category.Label;
                EditedWeight = category.Weight;
                Names = [];
                break;
            case DanceNode dance:
                EditedWeight = dance.Weight;
                Names =
                [
                    .. dance.Names.Select((name, index) =>
                        new DanceNameRow(name, index, index == 0, dance.Names.Count == 1))
                ];
                break;
            default:
                Names = [];
                break;
        }
    }

    private void ApplyMarks()
    {
        // A category that has since been deleted falls back to the whole list rather than leaving
        // the pick scoped to something that is not there.
        var marked = _markedKey is null ? null : FindByKey(Nodes, _markedKey);
        if (marked is null)
        {
            _markedKey = null;
        }

        foreach (var node in Flatten(Nodes))
        {
            node.IsMarked = ReferenceEquals(node, marked);
        }

        MarkedDescription = marked is null
            ? UiStrings.DanceList_MarkedWholeList
            : string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_MarkedFormat, marked.Label);
    }

    private static IEnumerable<DanceListNode> Flatten(IReadOnlyList<DanceListNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private void Rebuild(DanceListModel list)
    {
        _nodeSubscriptions.Clear();
        Nodes = BuildCategories(list.Categories, parentPath: [], keyPrefix: string.Empty);
        ApplyMarks();

        // Selection is restored by key rather than kept, because every edit replaces the whole tree.
        _restoringSelection = true;
        try
        {
            var restored = _selectedKey is null ? null : FindByKey(Nodes, _selectedKey);
            SelectedNode = restored;
            OnSelectionChanged(restored);
        }
        finally
        {
            _restoringSelection = false;
        }
    }

    private List<DanceListNode> BuildCategories(
        IReadOnlyList<DanceCategory> categories, int[] parentPath, string keyPrefix)
    {
        var nodes = new List<DanceListNode>(categories.Count);
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            int[] path = [.. parentPath, i];
            var key = $"{keyPrefix}c{i}/";

            var children = new List<DanceListNode>();
            children.AddRange(BuildCategories(category.Categories, path, key));
            children.AddRange(category.Dances.Select(dance => new DanceNode
            {
                Key = $"d:{dance.Slug}",
                Label = dance.DisplayName,
                Weight = dance.Weight,
                Slug = dance.Slug,
                CategoryPath = path,
                Names = dance.Names
            }));

            var node = new DanceCategoryNode
            {
                Key = key,
                Label = category.Name,
                Weight = category.Weight,
                Path = path,
                DanceCount = CountDances(category),
                Children = children,
                IsExpanded = _expandedKeys.Contains(key)
            };

            // Skip(1): the initial value is what this rebuild just restored, so replaying it would
            // only write back what it came from.
            _nodeSubscriptions.Add(node.WhenAnyValue(x => x.IsExpanded)
                .Skip(1)
                .Subscribe(expanded => NoteExpanded(node, expanded)));

            nodes.Add(node);
        }

        return nodes;
    }

    private static DanceListNode? FindByKey(IReadOnlyList<DanceListNode> nodes, string key)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Key, key, StringComparison.Ordinal))
            {
                return node;
            }

            var found = FindByKey(node.Children, key);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static int CountDances(DanceCategory category) =>
        category.Dances.Count + category.Categories.Sum(CountDances);

    private static int CountCategories(IReadOnlyList<DanceCategory> categories) =>
        categories.Sum(category => 1 + CountCategories(category.Categories));

    /// <summary>Remembers which categories are open, so an edit does not collapse the tree.</summary>
    private void NoteExpanded(DanceCategoryNode category, bool expanded)
    {
        if (expanded)
        {
            _expandedKeys.Add(category.Key);
        }
        else
        {
            _expandedKeys.Remove(category.Key);
        }
    }
}
