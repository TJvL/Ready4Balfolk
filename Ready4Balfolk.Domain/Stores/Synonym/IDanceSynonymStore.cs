using Ready4Balfolk.Domain.Models.Synonyms;

namespace Ready4Balfolk.Domain.Stores.Synonym;

public interface IDanceSynonymStore : IDisposable
{
    IReadOnlyList<DanceMainName> Current { get; }
    IObservable<IReadOnlyList<DanceMainName>> Observe();
    Task LoadAsync();
    Task UpdateAsync(Func<IReadOnlyList<DanceMainName>, IReadOnlyList<DanceMainName>> transform);
    Task ExportAsync(FileInfo destinationFileInfo);
    Task ImportAsync(FileInfo sourceFileInfo);
}
