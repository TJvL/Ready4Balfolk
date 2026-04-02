using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Synonym;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceSynonyms;

#pragma warning disable CS8618 // ObservableAsProperty fields set by helpers in constructor
public sealed partial class DanceSynonymsViewModel : ReactiveObject, IDisposable
{
    private readonly IDanceSynonymStore _store;
    private readonly IEditorHistoryService _editorHistory;
    private readonly INotificationService _notifications;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];
    private int _pendingCommits;
    private bool _pendingNewEntryEditMode;

    [Reactive] public partial IReadOnlyList<DanceSynonymEntryViewModel> Entries { get; set; }
    [Reactive] public partial bool IsLocked { get; private set; }

    [ObservableAsProperty] public partial bool IsLoading { get; }
    [ObservableAsProperty] public partial bool HasEntries { get; }
    [ObservableAsProperty] public partial string? UndoTooltip { get; }
    [ObservableAsProperty] public partial string? RedoTooltip { get; }

    private IObservable<bool> CanUndo => _editorHistory.CanUndo;
    private IObservable<bool> CanRedo => _editorHistory.CanRedo;

    [ReactiveCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRedoAsync(_editorHistory.UndoAsync);

    [ReactiveCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => UndoRedoAsync(_editorHistory.RedoAsync);

    [ReactiveCommand]
    private void AddLine()
    {
        if (IsLocked)
        {
            return;
        }

        _pendingNewEntryEditMode = true;
        CommitTracked(DanceSynonymAction.AddMainName(_store));
    }

    public DanceSynonymsViewModel(
        IDanceSynonymStore store,
        IEditorHistoryService editorHistory,
        INotificationService notifications,
        ILoggerService loggerService)
    {
        _store = store;
        _editorHistory = editorHistory;
        _notifications = notifications;
        _loggerService = loggerService;
        Entries = [];

        _isLoadingHelper = store.IsLoading
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        _hasEntriesHelper = this.WhenAnyValue(x => x.Entries)
            .Select(e => e.Count > 0)
            .ToProperty(this, x => x.HasEntries);
        _hasEntriesHelper.DisposeWith(_disposables);

        _undoTooltipHelper = editorHistory.UndoDescription
            .Select(desc => desc is not null ? string.Format(CultureInfo.CurrentCulture, UiStrings.DanceSynonyms_UndoFormat, desc) : UiStrings.DanceSynonyms_UndoDefault)
            .ToProperty(this, x => x.UndoTooltip);
        _undoTooltipHelper.DisposeWith(_disposables);

        _redoTooltipHelper = editorHistory.RedoDescription
            .Select(desc => desc is not null ? string.Format(CultureInfo.CurrentCulture, UiStrings.DanceSynonyms_RedoFormat, desc) : UiStrings.DanceSynonyms_RedoDefault)
            .ToProperty(this, x => x.RedoTooltip);
        _redoTooltipHelper.DisposeWith(_disposables);

        store.Observe()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => _pendingCommits == 0)
            .Subscribe(RebuildEntries)
            .DisposeWith(_disposables);
    }

    // --- Edit lock management ---

    private void SetLocked(int activeIndex)
    {
        IsLocked = true;
        for (var i = 0; i < Entries.Count; i++)
        {
            Entries[i].IsInteractionDisabled = i != activeIndex;
        }
    }

    private void ClearLock()
    {
        IsLocked = false;
        foreach (var entry in Entries)
        {
            entry.IsInteractionDisabled = false;
        }
    }

    // --- Name editing ---

    private void HandleStartEdit(int index)
    {
        if (IsLocked)
        {
            return;
        }

        if (index < 0 || index >= Entries.Count)
        {
            return;
        }

        Entries[index].IsEditing = true;
        SetLocked(index);
    }

    private void HandleConfirmEdit(int index)
    {
        if (index < 0 || index >= Entries.Count)
        {
            return;
        }

        var entry = Entries[index];
        var name = entry.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            _notifications.Show(UiStrings.DanceSynonyms_NameEmpty, NotificationSeverity.Warning);
            return;
        }

        if (!DanceSynonymTransforms.IsNameUnique(_store.Current, name, excludeMainIndex: index))
        {
            _notifications.Show(string.Format(CultureInfo.CurrentCulture, UiStrings.DanceSynonyms_NameInUse, name), NotificationSeverity.Warning);
            return;
        }

        entry.IsEditing = false;
        entry.IsNewlyAdded = false;
        ClearLock();

        if (name != entry.OriginalName)
        {
            CommitNameDirect(index, name);
        }
    }

    private void HandleCancelEdit(int index)
    {
        if (index < 0 || index >= Entries.Count)
        {
            return;
        }

        var entry = Entries[index];

        if (entry.IsNewlyAdded)
        {
            ClearLock();
            UndoRedoAsync(_editorHistory.UndoAsync);
        }
        else
        {
            entry.Name = entry.OriginalName!;
            entry.IsEditing = false;
            ClearLock();
        }
    }

    // --- Entry deletion ---

    private void HandleRemoveLine(int index)
    {
        if (IsLocked)
        {
            return;
        }

        CommitTracked(DanceSynonymAction.DeleteMainName(_store, index));
    }

    // --- Synonym management ---

    private void HandleRemoveSynonym(int index, int synIndex)
    {
        if (IsLocked)
        {
            return;
        }

        CommitTracked(DanceSynonymAction.DeleteSynonym(_store, index, synIndex));
    }

    private void HandleStartAddSynonym(int index)
    {
        if (IsLocked)
        {
            return;
        }

        if (index < 0 || index >= Entries.Count)
        {
            return;
        }

        var entry = Entries[index];
        entry.NewSynonymText = "";
        entry.IsAddingSynonym = true;
        SetLocked(index);
    }

    private void HandleConfirmAddSynonym(int index, string text)
    {
        if (index < 0 || index >= Entries.Count)
        {
            return;
        }

        var entry = Entries[index];
        entry.IsAddingSynonym = false;
        entry.NewSynonymText = "";
        ClearLock();

        if (!string.IsNullOrWhiteSpace(text))
        {
            CommitTracked(DanceSynonymAction.AddSynonymWithName(_store, index, text));
        }
    }

    private void HandleCancelAddSynonym(int index)
    {
        if (index < 0 || index >= Entries.Count)
        {
            return;
        }

        var entry = Entries[index];
        entry.IsAddingSynonym = false;
        entry.NewSynonymText = "";
        ClearLock();
    }

    // --- Store operations ---

    private async void CommitNameDirect(int index, string name)
    {
        _pendingCommits++;
        try
        {
            await _store.UpdateAsync(list =>
                DanceSynonymTransforms.RenameMainName(list, index, name));
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to save dance synonym changes", ex);
        }
        finally
        {
            _pendingCommits--;
        }
    }

    private async void CommitTracked(IEditorAction action)
    {
        _pendingCommits++;
        EditorActionResult result;
        try
        {
            result = await _editorHistory.DoActionAsync(action);
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to save dance synonym changes", ex);
            return;
        }
        finally
        {
            _pendingCommits--;
        }

        if (!result.Success)
        {
            _notifications.Show(result.ErrorMessage ?? "Action failed.", NotificationSeverity.Warning);
            return;
        }

        RebuildEntries(_store.Current);
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
            _ = _loggerService.ErrorAsync("Failed to undo/redo dance synonym changes", ex);
            return;
        }
        finally
        {
            _pendingCommits--;
        }

        RebuildEntries(_store.Current);
    }

    private void RebuildEntries(IReadOnlyList<DanceMainName> data)
    {
        foreach (var entry in Entries)
        {
            entry.Dispose();
        }

        var context = new DanceSynonymEntryContext(
            HandleStartEdit,
            HandleConfirmEdit,
            HandleCancelEdit,
            HandleRemoveLine,
            HandleRemoveSynonym,
            HandleStartAddSynonym,
            HandleConfirmAddSynonym,
            HandleCancelAddSynonym);

        Entries = data.Select((d, i) => new DanceSynonymEntryViewModel(d, i, context)).ToList();

        if (_pendingNewEntryEditMode && Entries.Count > 0)
        {
            _pendingNewEntryEditMode = false;
            var lastEntry = Entries[^1];
            lastEntry.IsEditing = true;
            lastEntry.IsNewlyAdded = true;
            SetLocked(Entries.Count - 1);
        }
    }

    public async Task ImportAsync(FileInfo file)
    {
        await _store.ImportAsync(file);
        _editorHistory.Clear();
        RebuildEntries(_store.Current);
    }

    public async Task ExportAsync(FileInfo file) => await _store.ExportAsync(file);

    public void Dispose()
    {
        _disposables.Dispose();
        foreach (var entry in Entries)
        {
            entry.Dispose();
        }
    }
}
