using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Auth.DTOs;

/// <summary>OAuth 2.0 client credentials token request (application/x-www-form-urlencoded, RFC 6749 §4.4).</summary>
public sealed class M2MTokenRequestDto
{
    [FromForm(Name = "grant_type")]
    public string? GrantType { get; init; }

    /// <summary>May instead be supplied via HTTP Basic authentication.</summary>
    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "client_secret")]
    public string? ClientSecret { get; init; }

    /// <summary>Space-delimited scopes. Defaults to all scopes allowed for the client.</summary>
    [FromForm(Name = "scope")]
    public string? Scope { get; init; }

    /// <summary>Target service. Optional when the client is allowed exactly one audience.</summary>
    [FromForm(Name = "audience")]
    public string? Audience { get; init; }
}

public sealed record M2MTokenResponseDto(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);

public sealed record OAuthErrorDto(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);

public sealed class CreateServiceClientDto
{
    [Required, MinLength(3), MaxLength(100)]
    public string ClientId { get; init; } = string.Empty;

    [MaxLength(255)]
    public string? Name { get; init; }

    public List<string> Scopes { get; init; } = [];

    [Required, MinLength(1)]
    public List<string> Audiences { get; init; } = [];
}

public sealed record ServiceClientDto(
    Guid Id,
    string ClientId,
    string Name,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedAudiences,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Returned once on creation / rotation. The secret is never retrievable afterwards.</summary>
public sealed record ServiceClientCredentialsDto(ServiceClientDto Client, string ClientSecret);
