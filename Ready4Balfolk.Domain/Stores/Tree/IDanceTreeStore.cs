using Ready4Balfolk.Domain.Models.Tree;

namespace Ready4Balfolk.Domain.Stores.Tree;

public interface IDanceTreeStore : IDisposable
{
    IReadOnlyList<DanceBranch> Current { get; }
    IObservable<IReadOnlyList<DanceBranch>> Observe();
    Task LoadAsync();
    Task UpdateAsync(Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>> transform);
    Task ExportAsync(FileInfo destinationFileInfo);
    Task ImportAsync(FileInfo sourceFileInfo);
}
