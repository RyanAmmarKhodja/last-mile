using System.Buffers.Binary;
using System.Collections.Concurrent;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging.Serialization;

/// <summary>
/// Confluent wire format for JSON Schema payloads: <c>[0x00][schema id: int32 big-endian][UTF-8 JSON]</c>.
/// Producers resolve (and in development register) the schema id per subject, and consumers strip the 5-byte header.
/// Messages stay plain JSON after the header, and kafka-ui decodes them through the registry.
/// </summary>
public sealed class EventSerializer(ISchemaRegistryClient registry, IOptions<MessagingOptions> options)
{
    private const byte MagicByte = 0;
    private const int HeaderSize = 5;
    private readonly ConcurrentDictionary<string, int> _ids = new();

    public async Task<int> GetSchemaIdAsync(string topic, string eventType, Type payloadType)
    {
        var subject = EventSchemas.Subject(topic, eventType);
        if (_ids.TryGetValue(subject, out var cached)) return cached;

        var schema = new Schema(EventSchemas.For(payloadType), SchemaType.Json);
        // Registering is idempotent: an identical schema returns the existing id, and an incompatible one fails here.
        var id = options.Value.AutoRegisterSchemas
            ? await registry.RegisterSchemaAsync(subject, schema, normalize: true)
            : (await registry.LookupSchemaAsync(subject, schema, ignoreDeletedSchemas: true, normalize: true)).Id;
        _ids[subject] = id;
        return id;
    }

    public static byte[] Frame(int schemaId, ReadOnlySpan<byte> json)
    {
        var bytes = new byte[HeaderSize + json.Length];
        bytes[0] = MagicByte;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1, 4), schemaId);
        json.CopyTo(bytes.AsSpan(HeaderSize));
        return bytes;
    }

    /// <summary>Splits a framed message. Plain JSON (no header) is accepted too, with <paramref name="schemaId"/> = null.</summary>
    public static ReadOnlyMemory<byte> Unframe(byte[] value, out int? schemaId)
    {
        if (value.Length >= HeaderSize && value[0] == MagicByte)
        {
            schemaId = BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(1, 4));
            return value.AsMemory(HeaderSize);
        }
        schemaId = null;
        return value;
    }
}
