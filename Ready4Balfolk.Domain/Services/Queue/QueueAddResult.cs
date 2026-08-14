using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed record QueueAddResult(
    bool Allowed,
    string? RejectionReason,
    Func<IQueueItem, bool>? RemovalPredicate,
    QueueDenial Denial = QueueDenial.Entry)
{
    public static QueueAddResult Allow(Func<IQueueItem, bool>? removalPredicate = null) =>
        new(true, null, removalPredicate);

    public static QueueAddResult Deny(string reason, QueueDenial denial = QueueDenial.Entry) =>
        new(false, reason, null, denial);
}
