using System.Text.Json.Serialization;

namespace LastMile.Auth.DTOs;

/// <summary>JSON Web Key Set (RFC 7517) containing the public signing keys.</summary>
public sealed record JwksDto([property: JsonPropertyName("keys")] IReadOnlyList<JwkDto> Keys);

public sealed record JwkDto(
    [property: JsonPropertyName("kty")] string Kty,
    [property: JsonPropertyName("use")] string Use,
    [property: JsonPropertyName("alg")] string Alg,
    [property: JsonPropertyName("kid")] string Kid,
    [property: JsonPropertyName("n")] string N,
    [property: JsonPropertyName("e")] string E);
