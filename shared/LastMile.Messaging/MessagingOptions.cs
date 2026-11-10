namespace LastMile.Messaging;

/// <summary>Configuration section <c>Kafka</c>.</summary>
public sealed class MessagingOptions
{
    public const string Section = "Kafka";

    /// <summary>e.g. <c>kafka:9092</c> inside docker, <c>localhost:9094</c> from the host.</summary>
    public string BootstrapServers { get; set; } = "localhost:9094";

    /// <summary>e.g. <c>http://schema-registry:8081</c> inside docker, <c>http://localhost:8081</c> from the host.</summary>
    public string SchemaRegistryUrl { get; set; } = "http://localhost:8081";

    /// <summary>
    /// Register a payload's schema on first publish. Convenient in development. In production, set it to false and
    /// register schemas from CI, so an incompatible change fails the pipeline instead of the publish.
    /// </summary>
    public bool AutoRegisterSchemas { get; set; } = true;

    public OutboxOptions Outbox { get; set; } = new();
    public ConsumerOptions Consumer { get; set; } = new();
}

public sealed class OutboxOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// How long published rows are kept before deletion. Keep it short: rows can carry personal data that the
    /// publishing service relays but does not own (e.g. registration profile fields).
    /// </summary>
    public TimeSpan PublishedRetention { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Age of the oldest unpublished row above which the health check reports Degraded.</summary>
    public TimeSpan HealthMaxLag { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class ConsumerOptions
{
    /// <summary>Consumer group. Defaults to the service name. All replicas of one service share it.</summary>
    public string? GroupId { get; set; }

    /// <summary>Attempts for a transient failure before the message goes to the DLQ (≈50 s total with the defaults).</summary>
    public int MaxAttempts { get; set; } = 8;
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Inbox rows older than this are purged. Matches the 30-day topic retention.</summary>
    public TimeSpan InboxRetention { get; set; } = TimeSpan.FromDays(30);
}
