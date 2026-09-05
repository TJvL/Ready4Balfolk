using System.Reactive.Linq;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Tests.Helpers;

/// <summary>A logger that keeps what it was told, and can be waited on.</summary>
/// <remarks>
/// The code under test starts work nobody awaits, so its report lands some time after the call
/// that provoked it returns. Waiting for the report is what makes the assertion about it real
/// rather than a race that happens to pass.
/// </remarks>
internal sealed class RecordingLoggerService : ILoggerService, IDisposable
{
    private readonly List<LogEntry> _errors = [];
    private readonly SemaphoreSlim _reported = new(0);

    /// <summary>How many reports have been handed out by <see cref="NextErrorAsync" />.</summary>
    private int _taken;

    public IObservable<LogEntry> WhenErrorLogged => Observable.Empty<LogEntry>();

    /// <summary>Everything reported so far.</summary>
    public IReadOnlyList<LogEntry> Errors
    {
        get
        {
            lock (_errors)
            {
                return [.. _errors];
            }
        }
    }

    /// <summary>Waits for the next report that has not been read yet, and returns that one.</summary>
    /// <remarks>
    /// The one whose release it took, rather than whichever is newest by the time the wait comes
    /// back: a second report landing in that window would otherwise be what the test asserts about,
    /// and the assertion would be about something the test never waited for. The wait is cancelled
    /// when it gives up, so it cannot be left pending to swallow a later report either.
    /// </remarks>
    public async Task<LogEntry> NextErrorAsync(CancellationToken cancellationToken)
    {
        using var givesUp = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        givesUp.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await _reported.WaitAsync(givesUp.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Nothing was reported within five seconds.");
        }

        lock (_errors)
        {
            return _errors[_taken++];
        }
    }

    public Task LogAsync(LogLevel logLevel, string message) => Task.CompletedTask;

    public Task DebugAsync(string message) => Task.CompletedTask;

    public Task InfoAsync(string message) => Task.CompletedTask;

    public Task WarningAsync(string message) => Task.CompletedTask;

    public Task ErrorAsync(string message) => Record(new LogEntry(LogLevel.Error, message));

    public Task ErrorAsync(string message, Exception exception) =>
        Record(new LogEntry(LogLevel.Error, message, exception));

    public Task CriticalAsync(string message, Exception exception) =>
        Record(new LogEntry(LogLevel.Critical, message, exception));

    public Task ExportAsync(string path) => Task.CompletedTask;

    public void Dispose() => _reported.Dispose();

    private Task Record(LogEntry entry)
    {
        lock (_errors)
        {
            _errors.Add(entry);
        }

        _reported.Release();
        return Task.CompletedTask;
    }
}
