namespace LastMile.Messaging.Inbox;

/// <summary>Row of the <c>inbox</c> table: an event this service has already processed (idempotency key).</summary>
public sealed class InboxMessage
{
    public Guid EventId { get; set; }
    public string Topic { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public DateTimeOffset ProcessedAt { get; set; }
}
