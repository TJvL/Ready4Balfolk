using System.Globalization;

namespace Ready4Balfolk.Domain.Models.QueueItems;

/// <summary>What names one row of the queue, for as long as it is in it.</summary>
/// <remarks>
/// A position is not a name. Every dance that ends takes the top row out and shifts every other one
/// up, so a request that travelled as "row three" acts on whatever row three has become by the time
/// it lands. The item itself is not a name either: queue items are records with value equality and
/// duplicates are ordinary, so two stops are the same value. This is the one thing that says which
/// row somebody was looking at.
/// </remarks>
public readonly record struct QueueItemId(Guid Value)
{
    public static QueueItemId New() => new(Guid.NewGuid());

    /// <summary>Reads one back off the wire, or says that it is not one.</summary>
    public static bool TryParse(string? text, out QueueItemId id)
    {
        if (Guid.TryParse(text, out var value))
        {
            id = new QueueItemId(value);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}
