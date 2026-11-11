using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LastMile.Shipments.Core;
using LastMile.Shipments.Options;
using Microsoft.Extensions.Options;

namespace LastMile.Shipments.Services.Clients;

/// <summary>
/// OAuth 2.0 client-credentials tokens for ShipmentService, one per (audience, scope), cached until one minute before
/// expiry. Thread-safe; concurrent callers share a single token request.
/// </summary>
public sealed class M2MTokenProvider(HttpClient http, IOptions<ServiceEndpointsOptions> options, ILogger<M2MTokenProvider> logger)
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<string> GetTokenAsync(string audience, string scope, CancellationToken ct)
    {
        var key = $"{audience}|{scope}";
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow) return cached.Token;

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow) return cached.Token;

            var o = options.Value;
            using var response = await http.PostAsync("api/v1/auth/m2m/token", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = o.ClientId,
                    ["client_secret"] = o.ClientSecret,
                    ["audience"] = audience,
                    ["scope"] = scope
                }), ct);
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException("AuthService",
                    $"token request for {audience} failed with {(int)response.StatusCode}");

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                        ?? throw new DependencyUnavailableException("AuthService", "empty token response");
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn) - RefreshMargin;
            _cache[key] = (token.AccessToken, expiresAt);
            logger.LogInformation("Obtained M2M token for {Audience} ({Scope}), valid until {ExpiresAt:O}", audience, scope, expiresAt);
            return token.AccessToken;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("AuthService", "token endpoint unreachable", ex);
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>Adds a cached M2M bearer token for one downstream audience to every request of a typed client.</summary>
public sealed class M2MTokenHandler(M2MTokenProvider tokens, string audience, string scope) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new("Bearer", await tokens.GetTokenAsync(audience, scope, ct));
        return await base.SendAsync(request, ct);
    }
}
