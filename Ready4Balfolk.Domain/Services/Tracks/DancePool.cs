using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <inheritdoc cref="IDancePool"/>
public sealed class DancePool : IDancePool, IDisposable
{
    private readonly BehaviorSubject<DancePoolSelection> _selection = new(DancePoolSelection.Everything);

    public IReadOnlyList<string> Tags => _selection.Value.Tags;

    public IReadOnlyList<string> ExcludedTags => _selection.Value.ExcludedTags;

    public RandomSelectionScope Scope => new RandomSelectionScope.Pool(Tags, ExcludedTags);

    public IObservable<DancePoolSelection> Observe() => _selection.AsObservable();

    /// <remarks>
    /// One click, three states: out, drawn from, never drawn. A separate control per direction
    /// would be two rails claiming to be about the same tags.
    /// </remarks>
    public void Toggle(string tag)
    {
        var tags = Tags.ToList();
        var excluded = ExcludedTags.ToList();

        if (tags.Remove(tag))
        {
            excluded.Add(tag);
        }
        else if (!excluded.Remove(tag))
        {
            tags.Add(tag);
        }

        _selection.OnNext(new DancePoolSelection(tags, excluded));
    }

    public void Clear()
    {
        if (!_selection.Value.IsEverything)
        {
            _selection.OnNext(DancePoolSelection.Everything);
        }
    }

    public void Dispose() => _selection.Dispose();
}
