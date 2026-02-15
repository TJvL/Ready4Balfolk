using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace Ready4Balfolk.UI.Services;

public class NotificationService : INotificationService, IDisposable
{
    private readonly SourceList<NotificationItem> _notifications = new();

    public ReadOnlyObservableCollection<NotificationItem> Notifications { get; }

    public NotificationService()
    {
        _notifications.Connect()
            .Bind(out var notifications)
            .Subscribe();
        Notifications = notifications;
    }

    public void Show(string message, NotificationSeverity severity)
    {
        const int maxNotifications = 5;
        while (_notifications.Count >= maxNotifications)
            _notifications.RemoveAt(0);

        var item = new NotificationItem(message, severity);
        _notifications.Add(item);

        Observable.Timer(TimeSpan.FromSeconds(4))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => _notifications.Remove(item));
    }

    public void Dismiss(NotificationItem item) => _notifications.Remove(item);

    public void Dispose()
    {
        _notifications.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed record NotificationItem(string Message, NotificationSeverity Severity);
