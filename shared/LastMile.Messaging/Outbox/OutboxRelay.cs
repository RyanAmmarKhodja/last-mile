using System.Text;
using Confluent.Kafka;
using LastMile.Contracts;
using LastMile.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging.Outbox;

/// <summary>
/// Publishes committed outbox rows to Kafka in id order, then marks them published and later deletes them.
/// Several replicas can run it: rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>.
/// Delivery is at least once. A crash after the Kafka ack but before the commit republishes the row, and
/// consumers de-duplicate on <c>eventId</c>.
/// </summary>
public sealed class OutboxRelay<TDbContext>(
    IServiceScopeFactory scopes,
    KafkaProducer kafka,
    EventSerializer serializer,
    IOptions<MessagingOptions> options,
    ILogger<OutboxRelay<TDbContext>> logger) : BackgroundService where TDbContext : DbContext
{
    private static readonly TimeSpan CleanupEvery = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var o = options.Value.Outbox;
        var nextCleanup = DateTimeOffset.UtcNow;
        logger.LogInformation("Outbox relay started (poll {Interval}, batch {Batch})", o.PollInterval, o.BatchSize);

        while (!ct.IsCancellationRequested)
        {
            var delay = o.PollInterval;
            try
            {
                var (published, failed) = await PublishBatchAsync(o.BatchSize, ct);
                if (failed) delay = FailureBackoff;
                else if (published == o.BatchSize) delay = TimeSpan.Zero; // backlog: keep draining

                if (DateTimeOffset.UtcNow >= nextCleanup)
                {
                    await DeletePublishedAsync(o.PublishedRetention, ct);
                    nextCleanup = DateTimeOffset.UtcNow + CleanupEvery;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox relay iteration failed");
                delay = FailureBackoff;
            }

            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<(int Published, bool Failed)> PublishBatchAsync(int batchSize, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.Set<OutboxMessage>()
            .FromSql($"""
                SELECT * FROM outbox
                WHERE published_at IS NULL
                ORDER BY id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);
        if (rows.Count == 0) return (0, false);

        var published = 0;
        var failed = false;
        foreach (var row in rows)
        {
            try
            {
                await PublishAsync(row, ct);
                row.PublishedAt = DateTimeOffset.UtcNow;
                row.LastError = null;
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                row.Attempts++;
                row.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                logger.LogWarning(ex, "Publishing outbox row {Id} ({Topic} {Type}) failed, attempt {Attempt}",
                    row.Id, row.Topic, row.EventType, row.Attempts);
                // Stop here: later rows may share this row's key and must not overtake it.
                failed = true;
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (published > 0) logger.LogDebug("Published {Count} outbox event(s)", published);
        return (published, failed);
    }

    private async Task PublishAsync(OutboxMessage row, CancellationToken ct)
    {
        var entry = EventCatalog.Find(row.Topic, row.EventType)
                    ?? throw new InvalidOperationException($"Unknown event {row.Topic}/{row.EventType}.");
        var schemaId = await serializer.GetSchemaIdAsync(row.Topic, row.EventType, entry.PayloadType);

        var headers = new Headers
        {
            { EventHeaders.Type, Encoding.UTF8.GetBytes(row.EventType) },
            { EventHeaders.SchemaVersion, Encoding.UTF8.GetBytes(entry.SchemaVersion.ToString()) },
            { EventHeaders.EventId, Encoding.UTF8.GetBytes(row.EventId.ToString()) }
        };
        if (row.CorrelationId is not null)
            headers.Add(EventHeaders.CorrelationId, Encoding.UTF8.GetBytes(row.CorrelationId));

        await kafka.Producer.ProduceAsync(row.Topic, new Message<string, byte[]>
        {
            Key = row.MessageKey,
            Value = EventSerializer.Frame(schemaId, Encoding.UTF8.GetBytes(row.Payload)),
            Headers = headers,
            Timestamp = new Timestamp(row.OccurredAt.UtcDateTime)
        }, ct);
    }

    private async Task DeletePublishedAsync(TimeSpan retention, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var cutoff = DateTimeOffset.UtcNow - retention;
        var deleted = await db.Set<OutboxMessage>()
            .Where(m => m.PublishedAt != null && m.PublishedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        if (deleted > 0) logger.LogInformation("Deleted {Count} published outbox row(s)", deleted);
    }
}
