namespace Ready4Balfolk.Domain.Services.Notifications;

/// <summary>Where the DJ is told something, in the language the application is in.</summary>
/// <remarks>
/// <para>
/// The screen and the log are two separate things with nothing coupling them. What is shown here
/// comes from the resx files like every other text the DJ reads; what is logged is English, written
/// as a literal where it is logged, and logging never puts anything on screen. A failure the DJ has
/// to hear about is therefore said twice, once to each, in two different texts.
/// </para>
/// <para>
/// Declared in the domain so a domain service can tell the DJ about a failure of its own rather
/// than leaving it to a log line somebody happens to turn into a notice. The application
/// implements it, may be asked from any thread, and never throws: it is reached on the way out of
/// something that already went wrong, including while the application is being composed and while
/// it is being torn down.
/// </para>
/// </remarks>
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
