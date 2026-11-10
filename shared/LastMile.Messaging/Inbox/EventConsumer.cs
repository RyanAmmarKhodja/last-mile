using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using LastMile.Contracts;
using LastMile.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging.Inbox;

/// <summary>
/// Base class for a service's Kafka consumer (documents/kafka-architecture.md §6). Subclasses list their topics
/// and implement <see cref="HandleAsync"/>. For every message, the base class:
/// <list type="number">
/// <item>opens a DB transaction and inserts the event id into <c>inbox</c>; a duplicate is skipped;</item>
/// <item>calls <see cref="HandleAsync"/>, then <c>SaveChanges</c> and commit, so handler and inbox are atomic;</item>
/// <item>commits the Kafka offset only after the DB commit (at-least-once);</item>
/// <item>retries transient failures with exponential backoff, in place, to keep per-key order;</item>
/// <item>sends permanent failures and exhausted retries to <c>&lt;topic&gt;.dlq</c> and moves on.</item>
/// </list>
/// Register with <c>services.AddEventConsumer&lt;TConsumer, TDbContext&gt;()</c>.
/// </summary>
public abstract class EventConsumer<TDbContext>(
    IServiceScopeFactory scopes,
    KafkaProducer kafka,
    MessagingService service,
    IOptions<MessagingOptions> options,
    ILogger logger) : BackgroundService where TDbContext : DbContext
{
    protected abstract IReadOnlyCollection<string> Topics { get; }

    /// <summary>
    /// Apply the event. <paramref name="db"/> and <paramref name="services"/> belong to the same DI scope, so anything
    /// resolved from <paramref name="services"/> (domain services, <c>IEventOutbox</c>, …) shares the DbContext and commits
    /// in the same transaction as the inbox row. Do not call SaveChanges; the base class does it.
    /// Events of types the handler does not care about can simply return.
    /// </summary>
    protected abstract Task HandleAsync(ConsumedEvent evt, TDbContext db, IServiceProvider services, CancellationToken ct);

    private string GroupId => options.Value.Consumer.GroupId ?? service.Name;

    protected override Task ExecuteAsync(CancellationToken ct) =>
        Task.Factory.StartNew(() => RunAsync(ct), ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken ct)
    {
        using var consumer = KafkaClients.CreateConsumer(options.Value, service, GroupId, logger);
        consumer.Subscribe(Topics);
        logger.LogInformation("Consumer group {Group} subscribed to {Topics}", GroupId, string.Join(", ", Topics));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;
                try
                {
                    result = consumer.Consume(ct);
                }
                catch (ConsumeException ex)
                {
                    // e.g. topic not created yet; librdkafka keeps retrying, so back off and poll again.
                    logger.LogWarning("Consume failed: {Reason}", ex.Error.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }
                if (result?.Message is null) continue;

                await ProcessAsync(result, ct);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessAsync(ConsumeResult<string, byte[]> result, CancellationToken ct)
    {
        ConsumedEvent evt;
        try
        {
            evt = Parse(result);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or PermanentEventException or KeyNotFoundException
                                       or InvalidOperationException)
        {
            await DeadLetterAsync(result, $"unparseable: {ex.Message}", ct);
            return;
        }

        var o = options.Value.Consumer;
        var backoff = o.InitialBackoff;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await HandleOnceAsync(evt, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is PermanentEventException or JsonException)
            {
                logger.LogError(ex, "Permanent failure for {Type} {EventId}", evt.Type, evt.EventId);
                await DeadLetterAsync(result, ex.Message, ct);
                return;
            }
            catch (Exception ex) when (attempt < o.MaxAttempts)
            {
                logger.LogWarning(ex, "Handling {Type} {EventId} failed (attempt {Attempt}/{Max}), retrying in {Delay}",
                    evt.Type, evt.EventId, attempt, o.MaxAttempts, backoff);
                await Task.Delay(backoff, ct);
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, o.MaxBackoff.Ticks));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Giving up on {Type} {EventId} after {Max} attempts", evt.Type, evt.EventId, o.MaxAttempts);
                await DeadLetterAsync(result, $"retries exhausted: {ex.Message}", ct);
                return;
            }
        }
    }

    private async Task HandleOnceAsync(ConsumedEvent evt, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO inbox (event_id, topic, event_type, processed_at)
            VALUES ({evt.EventId}, {evt.Topic}, {evt.Type}, {DateTimeOffset.UtcNow})
            ON CONFLICT (event_id) DO NOTHING
            """, ct);
        if (inserted == 0)
        {
            logger.LogInformation("Skipping duplicate {Type} {EventId}", evt.Type, evt.EventId);
            return; // the transaction is disposed without changes
        }

        await HandleAsync(evt, db, scope.ServiceProvider, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static ConsumedEvent Parse(ConsumeResult<string, byte[]> result)
    {
        var json = EventSerializer.Unframe(result.Message.Value, out var schemaId);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();
        return new ConsumedEvent
        {
            Topic = result.Topic,
            Partition = result.Partition.Value,
            Offset = result.Offset.Value,
            Key = result.Message.Key,
            EventId = root.GetProperty("eventId").GetGuid(),
            Type = root.GetProperty("type").GetString() ?? throw new PermanentEventException("Missing event type."),
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            SchemaId = schemaId,
            Envelope = root
        };
    }

    private async Task DeadLetterAsync(ConsumeResult<string, byte[]> result, string error, CancellationToken ct)
    {
        var headers = new Headers();
        foreach (var h in result.Message.Headers ?? []) headers.Add(h.Key, h.GetValueBytes());
        headers.Add(EventHeaders.DlqError, Encoding.UTF8.GetBytes(error.Length > 1000 ? error[..1000] : error));
        headers.Add(EventHeaders.DlqOriginalTopic, Encoding.UTF8.GetBytes(result.Topic));
        headers.Add(EventHeaders.DlqOriginalPartition, Encoding.UTF8.GetBytes(result.Partition.Value.ToString()));
        headers.Add(EventHeaders.DlqOriginalOffset, Encoding.UTF8.GetBytes(result.Offset.Value.ToString()));
        headers.Add(EventHeaders.DlqConsumerGroup, Encoding.UTF8.GetBytes(GroupId));

        await kafka.Producer.ProduceAsync(Contracts.Topics.DeadLetter(result.Topic), new Message<string, byte[]>
        {
            Key = result.Message.Key,
            Value = result.Message.Value,
            Headers = headers
        }, ct);
        logger.LogError("Dead-lettered {Topic}[{Partition}]@{Offset}: {Error}",
            result.Topic, result.Partition.Value, result.Offset.Value, error);
    }
}
