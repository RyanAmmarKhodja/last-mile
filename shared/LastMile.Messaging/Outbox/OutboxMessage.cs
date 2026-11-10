namespace LastMile.Messaging.Outbox;

/// <summary>Row of the <c>outbox</c> table: an event committed together with the business change that caused it.</summary>
public sealed class OutboxMessage
{
    /// <summary>Identity: publish order.</summary>
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string Topic { get; set; } = null!;
    public string MessageKey { get; set; } = null!;
    public string EventType { get; set; } = null!;
    /// <summary>The full JSON envelope (jsonb).</summary>
    public string Payload { get; set; } = null!;
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
