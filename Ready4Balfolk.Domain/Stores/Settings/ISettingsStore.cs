using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Stores.Settings;

public interface ISettingsStore : IDisposable
{
    ApplicationSettings Current { get; }
    IObservable<ApplicationSettings> Observe();
    Task UpdateAsync(Func<ApplicationSettings, ApplicationSettings> transform);
}
