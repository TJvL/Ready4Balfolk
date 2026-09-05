using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Services.Audio;

/// <summary>Whether there is still an output to play on, and telling the DJ when there is not.</summary>
/// <remarks>
/// BASS refuses to start a channel on a device that is no longer there, which is what an interface
/// unplugged mid-set looks like from in here. The honest thing to say then is that the audio is
/// gone, not that this one dance would not play: the DJ has to go and look at the cable, and the
/// screens and the phone have to stop showing a dance the hall cannot hear.
///
/// Said once per change. A night whose output has gone keeps being asked to play, and a
/// notification per attempt would bury the one that meant something.
/// </remarks>
public sealed class AudioAvailability(ILoggerService loggerService) : IDisposable
{
    private readonly BehaviorSubject<bool> _isAvailable = new(true);

    public IObservable<bool> WhenChanged => _isAvailable.AsObservable();

    /// <summary>Sound is on its way, so an output that had gone is back.</summary>
    public void Working()
    {
        if (_isAvailable.Value)
        {
            return;
        }

        _isAvailable.OnNext(true);
        _ = loggerService.InfoAsync("Audio output is available again");
    }

    /// <summary>Nothing came out, so there is no output until something plays again.</summary>
    /// <param name="detail">What BASS said, which is for the log rather than for the DJ.</param>
    public void Gone(string detail)
    {
        if (!_isAvailable.Value)
        {
            return;
        }

        _isAvailable.OnNext(false);
        loggerService.Report(DomainStrings.Audio_OutputGone, new InvalidOperationException(detail));
    }

    /// <summary>The output never came up at all, so there was never anything to lose.</summary>
    public void NeverCameUp(Exception cause)
    {
        _isAvailable.OnNext(false);
        _ = loggerService.CriticalAsync("Failed to initialize BASS audio", cause);
    }

    public void Dispose() => _isAvailable.Dispose();
}
