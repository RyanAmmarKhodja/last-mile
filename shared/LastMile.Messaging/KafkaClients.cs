using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging;

/// <summary>Kafka client configurations (documents/kafka-architecture.md §5–§6).</summary>
internal static class KafkaClients
{
    public static IProducer<string, byte[]> CreateProducer(MessagingOptions o, MessagingService service, ILogger logger) =>
        new ProducerBuilder<string, byte[]>(new ProducerConfig
            {
                BootstrapServers = o.BootstrapServers,
                ClientId = service.Name,
                Acks = Acks.All,
                EnableIdempotence = true,
                MaxInFlight = 5,
                LingerMs = 5,
                CompressionType = CompressionType.Lz4,
                MessageTimeoutMs = 30_000
            })
            .SetLogHandler((_, m) => logger.Log(Map(m.Level), "kafka producer: {Message}", m.Message))
            .SetErrorHandler((_, e) => logger.LogWarning("kafka producer error: {Reason}", e.Reason))
            .Build();

    public static IConsumer<string, byte[]> CreateConsumer(MessagingOptions o, MessagingService service, string groupId,
        ILogger logger) =>
        new ConsumerBuilder<string, byte[]>(new ConsumerConfig
            {
                BootstrapServers = o.BootstrapServers,
                ClientId = service.Name,
                GroupId = groupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                IsolationLevel = IsolationLevel.ReadCommitted,
                // Retries with backoff happen inside the poll loop; leave room for them.
                MaxPollIntervalMs = 600_000
            })
            .SetLogHandler((_, m) => logger.Log(Map(m.Level), "kafka consumer: {Message}", m.Message))
            .SetErrorHandler((_, e) => logger.LogWarning("kafka consumer error: {Reason}", e.Reason))
            .Build();

    private static LogLevel Map(SyslogLevel level) => level switch
    {
        SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical or SyslogLevel.Error => LogLevel.Error,
        SyslogLevel.Warning => LogLevel.Warning,
        SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
        _ => LogLevel.Debug
    };
}

/// <summary>Process-wide idempotent producer (outbox relay and DLQ). Disposed with the host.</summary>
public sealed class KafkaProducer(IOptions<MessagingOptions> options, MessagingService service, ILogger<KafkaProducer> logger)
    : IDisposable
{
    private readonly Lazy<IProducer<string, byte[]>> _producer =
        new(() => KafkaClients.CreateProducer(options.Value, service, logger));

    public IProducer<string, byte[]> Producer => _producer.Value;

    public void Dispose()
    {
        if (!_producer.IsValueCreated) return;
        _producer.Value.Flush(TimeSpan.FromSeconds(5));
        _producer.Value.Dispose();
    }
}
