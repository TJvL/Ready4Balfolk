using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Synonyms;

namespace Ready4Balfolk.UI.Views.DanceSynonyms;

public sealed record DanceSynonymEntryContext(
    Action<int> StartEdit,
    Action<int> ConfirmEdit,
    Action<int> CancelEdit,
    Action<int> RemoveLine,
    Action<int, int> RemoveSynonym,
    Action<int> StartAddSynonym,
    Action<int, string> ConfirmAddSynonym,
    Action<int> CancelAddSynonym);

public sealed partial class DanceSynonymEntryViewModel : ReactiveObject, IDisposable
{
    private readonly DanceSynonymEntryContext _context;
    private readonly CompositeDisposable _disposables = [];
    private readonly int _index;

    public IReadOnlyList<string> Synonyms { get; }

    [Reactive] public partial string Name { get; set; }
    [Reactive] public partial bool IsEditing { get; set; }
    [Reactive] public partial bool IsNewlyAdded { get; set; }
    [Reactive] public partial bool IsAddingSynonym { get; set; }
    [Reactive] public partial string NewSynonymText { get; set; }
    [Reactive] public partial bool IsInteractionDisabled { get; set; }

    public string? OriginalName { get; private set; }

    [ReactiveCommand]
    private void ToggleEditing()
    {
        if (IsEditing)
        {
            _context.ConfirmEdit(_index);
        }
        else
        {
            _context.StartEdit(_index);
        }
    }

    [ReactiveCommand]
    private void DeleteOrCancel()
    {
        if (IsEditing)
        {
            _context.CancelEdit(_index);
        }
        else
        {
            _context.RemoveLine(_index);
        }
    }

    [ReactiveCommand]
    private void RemoveSynonym(string synonym)
    {
        var synIndex = Synonyms.ToList().IndexOf(synonym);
        if (synIndex >= 0)
        {
            _context.RemoveSynonym(_index, synIndex);
        }
    }

    [ReactiveCommand]
    private void StartAddSynonym() => _context.StartAddSynonym(_index);

    [ReactiveCommand]
    private void ConfirmAddSynonym() => _context.ConfirmAddSynonym(_index, NewSynonymText.Trim());

    [ReactiveCommand]
    private void CancelAddSynonym() => _context.CancelAddSynonym(_index);

    public void ConfirmEdit() => _context.ConfirmEdit(_index);
    public void CancelEdit() => _context.CancelEdit(_index);
    public void RequestConfirmAddSynonym() => _context.ConfirmAddSynonym(_index, NewSynonymText.Trim());
    public void RequestCancelAddSynonym() => _context.CancelAddSynonym(_index);

    public DanceSynonymEntryViewModel(DanceMainName data, int index, DanceSynonymEntryContext context)
    {
        _context = context;
        _index = index;
        Name = data.Name;
        NewSynonymText = "";
        Synonyms = data.Synonyms.Select(s => s.Name).ToList();

        this.WhenAnyValue(x => x.IsEditing)
            .Where(editing => editing)
            .Subscribe(_ => OriginalName = Name)
            .DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}
