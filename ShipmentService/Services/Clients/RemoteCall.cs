using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LastMile.Shipments.Core;

namespace LastMile.Shipments.Services.Clients;

/// <summary>
/// Shared GET helper for the typed clients: 404 → null; any other failure (5xx, 401/403 misconfiguration, timeout,
/// connection refused, bad JSON) → <see cref="DependencyUnavailableException"/> (503), so callers fail fast (NFR-3).
/// </summary>
internal static class RemoteCall
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T?> GetAsync<T>(HttpClient http, string service, string path, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException(service, $"GET {path} returned {(int)response.StatusCode}");
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                   ?? throw new DependencyUnavailableException(service, $"GET {path} returned an empty body");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException(service, ex is TaskCanceledException ? "timed out" : ex.Message, ex);
        }
    }
}
