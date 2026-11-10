using System.Text.Json;
using System.Text.Json.Serialization;

namespace LastMile.Contracts;

/// <summary>
/// The one JSON convention for integration events: camelCase, enums as strings, nullable annotations and
/// required constructor parameters enforced. Producers, consumers and the schema generator all use it.
/// </summary>
public static class ContractJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            // Consumers must tolerate fields added by newer producers (evolution rule, kafka-architecture.md §4).
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };
        o.Converters.Add(new JsonStringEnumConverter());
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }
}
