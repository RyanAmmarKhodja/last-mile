using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using LastMile.Contracts;

namespace LastMile.Messaging.Serialization;

/// <summary>
/// JSON Schema of an event's full envelope (<see cref="EventEnvelope{TData}"/>), generated from the C# contract
/// with <see cref="ContractJson.Options"/>. The schema therefore always matches what is actually serialized.
/// </summary>
public static class EventSchemas
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    /// <summary>Schema Registry subject (TopicRecordNameStrategy style): <c>&lt;topic&gt;-&lt;EventType&gt;</c>.</summary>
    public static string Subject(string topic, string eventType) => $"{topic}-{eventType}";

    public static string For(Type payloadType) => Cache.GetOrAdd(payloadType, Generate);

    private static string Generate(Type payloadType)
    {
        var entry = EventCatalog.For(payloadType);
        var envelopeType = typeof(EventEnvelope<>).MakeGenericType(payloadType);
        var schema = ContractJson.Options.GetJsonSchemaAsNode(envelopeType, new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true
        }).AsObject();

        // The generic Data property is null-oblivious, but every event has a payload.
        if (schema["properties"]?["data"] is JsonObject data) data["type"] = "object";

        var root = new JsonObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = entry.Type,
            ["description"] = $"{entry.Topic} {entry.Type} v{entry.SchemaVersion}"
        };
        foreach (var (key, value) in schema.ToList())
        {
            schema.Remove(key);
            root[key] = value;
        }
        return root.ToJsonString();
    }
}
