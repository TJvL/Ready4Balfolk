using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <inheritdoc cref="IDancePool"/>
public sealed class DancePool : IDancePool, IDisposable
{
    private readonly BehaviorSubject<IReadOnlyList<string>> _tags = new([]);

    public IReadOnlyList<string> Tags => _tags.Value;

    public RandomSelectionScope Scope => new RandomSelectionScope.Pool(Tags);

    public IObservable<IReadOnlyList<string>> Observe() => _tags.AsObservable();

    public void Toggle(string tag)
    {
        var tags = Tags.ToList();
        if (!tags.Remove(tag))
        {
            tags.Add(tag);
        }

        _tags.OnNext(tags);
    }

    public void Clear()
    {
        if (Tags.Count > 0)
        {
            _tags.OnNext([]);
        }
    }

    public void Dispose() => _tags.Dispose();
}
