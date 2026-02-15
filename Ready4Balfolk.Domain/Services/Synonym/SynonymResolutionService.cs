using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Stores.Synonym;

namespace Ready4Balfolk.Domain.Services.Synonym;

public sealed class SynonymResolutionService : ISynonymResolutionService, IDisposable
{
    private readonly Subject<Unit> _changed = new();
    private readonly IDisposable _subscription;
    private Dictionary<string, string> _lookup;

    public SynonymResolutionService(IDanceSynonymStore synonymStore)
    {
        _lookup = BuildLookup(synonymStore.Current);

        _subscription = synonymStore.Observe()
            .Skip(1)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(OnSynonymsChanged);
    }

    public IObservable<Unit> Changed => _changed;

    public string Resolve(string danceName)
    {
        var normalized = StringNormalizer.Normalize(danceName);
        return _lookup.GetValueOrDefault(normalized, danceName);
    }

    public bool IsKnown(string danceName)
    {
        var normalized = StringNormalizer.Normalize(danceName);
        return _lookup.ContainsKey(normalized);
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _changed.Dispose();
    }

    private void OnSynonymsChanged(IReadOnlyList<DanceMainName> synonyms)
    {
        var newLookup = BuildLookup(synonyms);
        Interlocked.Exchange(ref _lookup, newLookup);
        _changed.OnNext(Unit.Default);
    }

    private static Dictionary<string, string> BuildLookup(IEnumerable<DanceMainName> synonyms)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mainName in synonyms)
        {
            var normalizedMain = StringNormalizer.Normalize(mainName.Name);
            lookup[normalizedMain] = mainName.Name;

            foreach (var synonym in mainName.Synonyms)
            {
                var normalizedSynonym = StringNormalizer.Normalize(synonym.Name);
                lookup[normalizedSynonym] = mainName.Name;
            }
        }

        return lookup;
    }
}
