using System.Text.Json;
using LastMile.Contracts;

namespace LastMile.Messaging.Inbox;

/// <summary>A received integration event: Kafka coordinates plus the parsed envelope.</summary>
public sealed class ConsumedEvent
{
    public required string Topic { get; init; }
    public required int Partition { get; init; }
    public required long Offset { get; init; }
    public required string Key { get; init; }
    public required Guid EventId { get; init; }
    public required string Type { get; init; }
    public required int SchemaVersion { get; init; }
    public int? SchemaId { get; init; }
    public required JsonElement Envelope { get; init; }

    /// <summary>Deserializes the whole envelope. Unknown (newer) fields are ignored.</summary>
    public EventEnvelope<TPayload> As<TPayload>() =>
        Envelope.Deserialize<EventEnvelope<TPayload>>(ContractJson.Options)
        ?? throw new PermanentEventException($"Event {EventId} has an empty envelope.");

    /// <summary>Deserializes only the payload (<c>data</c>).</summary>
    public TPayload Data<TPayload>() => As<TPayload>().Data;

    /// <summary>True when this is the event bound to <typeparamref name="TPayload"/> by [IntegrationEvent].</summary>
    public bool Is<TPayload>()
    {
        var entry = EventCatalog.For(typeof(TPayload));
        return entry.Topic == Topic && entry.Type == Type;
    }
}

/// <summary>
/// Throw from a handler when retrying cannot help: invalid payload or broken invariant. The message goes straight
/// to the dead-letter topic. A business rejection that the producer must learn about should instead be published
/// as a domain event (e.g. <c>DriverProfileRejected</c>), with the handler returning normally.
/// </summary>
public sealed class PermanentEventException(string message, Exception? inner = null) : Exception(message, inner);
