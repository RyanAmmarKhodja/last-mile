using System.Diagnostics;
using System.Text.Json;
using LastMile.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Messaging.Outbox;

/// <summary>Adds integration events to the current unit of work. They are published only if the transaction commits.</summary>
public interface IEventOutbox
{
    /// <summary>
    /// Stages <paramref name="payload"/> in the outbox of the scoped DbContext. It is written by the caller's next
    /// <c>SaveChanges</c>, in the same transaction as the business change. Topic and type come from the payload's
    /// <see cref="IntegrationEventAttribute"/>.
    /// </summary>
    /// <param name="subject">Aggregate id, used as the Kafka message key.</param>
    Guid Enqueue<TPayload>(string subject, TPayload payload) where TPayload : notnull;
}

public sealed class EventOutbox<TDbContext>(TDbContext db, MessagingService service, IHttpContextAccessor? http = null)
    : IEventOutbox where TDbContext : DbContext
{
    public const string RequestIdHeader = "X-Request-Id";

    public Guid Enqueue<TPayload>(string subject, TPayload payload) where TPayload : notnull
    {
        var entry = EventCatalog.For(typeof(TPayload));
        var eventId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var correlationId = CorrelationId();

        var envelope = new EventEnvelope<TPayload>(eventId, entry.Type, entry.SchemaVersion, service.Name, now,
            correlationId, subject, payload);

        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            EventId = eventId,
            Topic = entry.Topic,
            MessageKey = subject,
            EventType = entry.Type,
            Payload = JsonSerializer.Serialize(envelope, ContractJson.Options),
            CorrelationId = correlationId,
            OccurredAt = now
        });
        return eventId;
    }

    private string? CorrelationId()
    {
        var ctx = http?.HttpContext;
        if (ctx is not null && ctx.Request.Headers.TryGetValue(RequestIdHeader, out var requestId) &&
            !string.IsNullOrWhiteSpace(requestId))
            return requestId.ToString()[..Math.Min(100, requestId.ToString().Length)];
        return Activity.Current?.TraceId.ToString() ?? ctx?.TraceIdentifier;
    }
}
