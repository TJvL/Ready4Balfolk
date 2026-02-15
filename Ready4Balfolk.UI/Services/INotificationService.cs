namespace Ready4Balfolk.UI.Services;

public interface INotificationService
{
    void Show(string message, NotificationSeverity severity);
}

public enum NotificationSeverity
{
    Information,
    Warning,
    Error
}
