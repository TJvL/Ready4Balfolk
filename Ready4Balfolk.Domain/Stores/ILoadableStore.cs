namespace Ready4Balfolk.Domain.Stores;

public interface ILoadableStore
{
    IObservable<bool> IsLoading { get; }
}
