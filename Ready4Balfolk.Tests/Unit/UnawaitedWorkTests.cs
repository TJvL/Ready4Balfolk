using System.Globalization;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>Where the failure of work nobody awaits ends up, and how often.</summary>
/// <remarks>
/// One class, because it is one answer: what the mechanism itself does with an exception, what a
/// view handler that leans on it resolves its logger and its notifications from, and what a report
/// the application did not ask for would come out of.
/// </remarks>
public sealed class UnawaitedWorkTests
{
    /// <summary>A handler's failure is an English line in the log and the resx text on screen.</summary>
    /// <remarks>
    /// The view code-behind used to hand its translated text to the logger, so the Dutch sentence
    /// was the log line and the English one-off literals elsewhere were the notices. Said in Dutch
    /// here, where the two texts cannot pass for each other.
    /// </remarks>
    [Fact]
    public async Task Run_WhenTheWorkFails_LogsTheEnglishLineAndShowsTheResxText()
    {
        using var logger = new RecordingLoggerService();
        var notifications = Substitute.For<INotificationService>();
        await using var services = Containing(logger, notifications);
        App.UseServices(services);
        var language = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("nl");
        try
        {
            var shown = UiStrings.Review_PreviewFailed;

            Handlers.Run(
                "Failed to preview the track",
                shown,
                () => throw new InvalidOperationException("Failed to create stream"));

            var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Failed to preview the track", reported.Message);
            Assert.IsType<InvalidOperationException>(reported.Exception);
            Assert.Equal("Het nummer kon niet beluisterd worden", shown);
            notifications.Received(1).Show(shown, NotificationSeverity.Error);
        }
        finally
        {
            CultureInfo.CurrentUICulture = language;
        }
    }

    [Fact]
    public async Task Run_WhenTheContainerIsAlreadyGone_StillDoesNotThrow()
    {
        var services = Containing(new NoOpLoggerService(), Substitute.For<INotificationService>());
        await services.DisposeAsync();
        App.UseServices(services);

        var ran = new TaskCompletionSource();

        // A pointer that moves while the window is being torn down. Resolving the logger is the
        // one step of this that can throw, and it happens in a void handler: outside everything
        // the mechanism provides, which would make it the crash the mechanism exists to prevent.
        Assert.Null(Record.Exception(() => Handlers.Run(
            "Failed to move the queue item",
            UiStrings.Queue_MoveItemFailed,
            () =>
            {
                ran.SetResult();
                return Task.CompletedTask;
            })));

        // And the work itself still runs. Having nowhere left to report to is not a reason to
        // drop what the person asked for.
        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OneFailureAtASiteThatReportsItsOwn_ReachesTheLogExactlyOnce()
    {
        using var logger = new RecordingLoggerService();
        await using var services = Containing(logger, Substitute.For<INotificationService>());

        // The container is what a report the application did not ask for would come out of, which
        // is how this notices one.
        App.UseServices(services);

        // AsyncAwaitBestPractices runs a process-wide default handler in addition to the handler a
        // call site passes. The application has a couple of dozen sites that already say what they
        // could not do, so a default alongside them is two log lines and two notices for one thing
        // going wrong.
        SafeFireAndForgetExtensions.SetDefaultExceptionHandling(exception =>
            logger.Report("Unhandled fire-and-forget exception", exception));

        try
        {
            ApplicationComposition.UseOneReportPerFailure();

            // What saving a setting onto a read-only file looks like from here.
            Task.FromException(new UnauthorizedAccessException("settings.json is read only"))
                .SafeFireAndForget(exception => logger.Report("Failed to save settings", exception));

            var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Failed to save settings", reported.Message);
            Assert.Single(logger.Errors);
        }
        finally
        {
            SafeFireAndForgetExtensions.RemoveDefaultExceptionHandling();
        }
    }

    [Fact]
    public async Task Start_WorkThrowsAfterAnAwait_ReportsItAndNothingEscapes()
    {
        using var logger = new RecordingLoggerService();
        var notifications = Substitute.For<INotificationService>();
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;

        // Set while the work is started, because an async void sends what it could not return to
        // whichever context was current when it began. That is the route this has to close.
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            new UnawaitedWork(logger, notifications).Start(
                "Failed to preview the track", "Het nummer kon niet beluisterd worden", async () =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Failed to create stream");
                });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to preview the track", reported.Message);
        Assert.IsType<InvalidOperationException>(reported.Exception);
        notifications.Received(1).Show("Het nummer kon niet beluisterd worden", NotificationSeverity.Error);
        Assert.Empty(context.Escaped);
    }

    [Fact]
    public async Task Start_WorkThrowsBeforeItReturnsATask_ReportsIt()
    {
        using var logger = new RecordingLoggerService();

        // Nothing on the returned task could catch this one: there is no returned task. It is the
        // reason the work is wrapped rather than handed a SafeFireAndForget handler.
        new UnawaitedWork(logger, notifications: null).Start(
            "Failed to export the log",
            "Het logbestand kon niet geëxporteerd worden",
            () => throw new IOException("the folder cannot be written"));

        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to export the log", reported.Message);
        Assert.IsType<IOException>(reported.Exception);
    }

    [Fact]
    public async Task Start_WorkSucceeds_ReportsNothing()
    {
        using var logger = new RecordingLoggerService();
        var notifications = Substitute.For<INotificationService>();
        var ran = new TaskCompletionSource();

        new UnawaitedWork(logger, notifications).Start(
            "Failed to preview the track", "Het nummer kon niet beluisterd worden", () =>
            {
                ran.SetResult();
                return Task.CompletedTask;
            });

        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Empty(logger.Errors);
        notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
    }

    private static ServiceProvider Containing(ILoggerService logger, INotificationService notifications) =>
        new ServiceCollection().AddSingleton(logger).AddSingleton(notifications).BuildServiceProvider();

    /// <summary>Catches what an async void could not return, which is where the crash came from.</summary>
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly List<Exception> _escaped = [];

        public IReadOnlyList<Exception> Escaped
        {
            get
            {
                lock (_escaped)
                {
                    return [.. _escaped];
                }
            }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);

            try
            {
                d(state);
            }
            catch (Exception exception)
            {
                lock (_escaped)
                {
                    _escaped.Add(exception);
                }
            }
        }
    }
}
