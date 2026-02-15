using System.Reactive;

namespace Ready4Balfolk.Domain.Services.Synonym;

public interface ISynonymResolutionService
{
    /// <summary>
    /// Resolves a dance name to its canonical main name.
    /// Returns the canonical name if found, or the input unchanged if not.
    /// </summary>
    string Resolve(string danceName);

    /// <summary>
    /// Returns true if the dance name (or a synonym) is known.
    /// </summary>
    bool IsKnown(string danceName);

    /// <summary>
    /// Emits when the synonym lookup is rebuilt (synonym data changed).
    /// </summary>
    IObservable<Unit> Changed { get; }
}
