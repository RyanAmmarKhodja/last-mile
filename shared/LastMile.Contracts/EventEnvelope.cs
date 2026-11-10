namespace LastMile.Contracts;

/// <summary>
/// The JSON envelope every integration event travels in (documents/kafka-architecture.md §4).
/// The Kafka message key is <see cref="Subject"/> (the aggregate id), which keeps per-aggregate ordering.
/// </summary>
/// <param name="EventId">Unique per event; consumers de-duplicate on it.</param>
/// <param name="Type">Event type name, e.g. <c>UserRegistered</c>. Also sent as the <c>type</c> header.</param>
/// <param name="SchemaVersion">Additive revision of the payload within the topic's major version.</param>
/// <param name="Source">Publishing service, e.g. <c>auth-service</c>.</param>
/// <param name="OccurredAt">When the change was committed by the owner.</param>
/// <param name="CorrelationId">Id of the HTTP request that caused the event, for tracing.</param>
/// <param name="Subject">Aggregate id (= message key).</param>
/// <param name="Data">The event payload.</param>
public sealed record EventEnvelope<TData>(
    Guid EventId,
    string Type,
    int SchemaVersion,
    string Source,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string Subject,
    TData Data);

/// <summary>Kafka header names set on every integration event.</summary>
public static class EventHeaders
{
    public const string Type = "type";
    public const string SchemaVersion = "schemaVersion";
    public const string EventId = "eventId";
    public const string CorrelationId = "correlationId";
    public const string Source = "source";

    // Added by consumers when a message is dead-lettered.
    public const string DlqError = "dlq-error";
    public const string DlqOriginalTopic = "dlq-original-topic";
    public const string DlqOriginalPartition = "dlq-original-partition";
    public const string DlqOriginalOffset = "dlq-original-offset";
    public const string DlqConsumerGroup = "dlq-consumer-group";
}
