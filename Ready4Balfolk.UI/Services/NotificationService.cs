using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Services.Notifications;

namespace Ready4Balfolk.UI.Services;

/// <summary>The bars along the bottom of the window, which is the one place the DJ is told things.</summary>
/// <remarks>
/// <para>
/// Asked from anywhere: a view model on the UI thread, and a store, the audio engine or a scan on
/// whichever thread they were on when something failed. The list the overlay binds to is only ever
/// changed on the UI thread, so everything is put there first.
/// </para>
/// <para>
/// An error that is already on screen is not shown again beside itself. A watcher that keeps
/// failing, or a handler that catches the same thing on every tick, would otherwise fill the five
/// bars with one sentence and push out whatever else there was to say.
/// </para>
/// <para>
/// A bar is said before there is a window to say it in when the failure happens while the
/// application is still being put together: a settings file that would not read, an audio device
/// that would not open. Those are kept, and their four seconds start when the window opens.
/// </para>
/// </remarks>
public class NotificationService : INotificationService, IDisposable
{
    private const int MaxNotifications = 5;

    /// <summary>How long a bar stays up once there is a window to read it in.</summary>
    private static readonly TimeSpan OnScreenFor = TimeSpan.FromSeconds(4);

    private readonly SourceList<NotificationItem> _notifications = new();
    private readonly IScheduler _clock;
    private readonly List<NotificationItem> _waitingForTheWindow = [];
    private bool _windowIsOpen;
    private volatile bool _disposed;

    public ReadOnlyObservableCollection<NotificationItem> Notifications { get; }

    public NotificationService() : this(DefaultScheduler.Instance)
    {
    }

    /// <summary>The same bars, with their four seconds counted on a clock a test can move.</summary>
    internal NotificationService(IScheduler clock)
    {
        _clock = clock;
        _notifications.Connect()
            .Bind(out var notifications)
            .Subscribe();
        Notifications = notifications;
    }

    /// <summary>Puts a bar up, on the UI thread, from whichever thread asked.</summary>
    /// <remarks>Never throws: see <see cref="INotificationService" />.</remarks>
    public void Show(string message, NotificationSeverity severity)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            RxSchedulers.MainThreadScheduler.Schedule(() => Add(message, severity));
        }
        catch (Exception)
        {
            // On the way down the dispatcher can be gone before the last failure is reported, and
            // a bar nobody will see is not worth an exception out of the code that reported it.
        }
    }

    /// <summary>The window is up, so the bars said before it was start counting down.</summary>
    public void WindowOpened()
    {
        _windowIsOpen = true;

        foreach (var item in _waitingForTheWindow)
        {
            DismissLater(item);
        }

        _waitingForTheWindow.Clear();
    }

    public void Dismiss(NotificationItem item) => _notifications.Remove(item);

    public void Dispose()
    {
        _disposed = true;
        _notifications.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Add(string message, NotificationSeverity severity)
    {
        if (_disposed)
        {
            return;
        }

        if (severity is NotificationSeverity.Error
            && _notifications.Items.Any(shown => shown.Severity == severity
                && string.Equals(shown.Message, message, StringComparison.Ordinal)))
        {
            return;
        }

        while (_notifications.Count >= MaxNotifications)
        {
            var oldest = _notifications.Items[0];
            _notifications.RemoveAt(0);
            _waitingForTheWindow.Remove(oldest);
        }

        var item = new NotificationItem(message, severity);
        _notifications.Add(item);

        if (_windowIsOpen)
        {
            DismissLater(item);
        }
        else
        {
            _waitingForTheWindow.Add(item);
        }
    }

    private void DismissLater(NotificationItem item) =>
        Observable.Timer(OnScreenFor, _clock)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ =>
            {
                if (!_disposed)
                {
                    _notifications.Remove(item);
                }
            });
}

public sealed record NotificationItem(string Message, NotificationSeverity Severity);
