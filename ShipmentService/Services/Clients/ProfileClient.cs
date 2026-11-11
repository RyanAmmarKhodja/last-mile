namespace LastMile.Shipments.Services.Clients;

/// <summary>ProfileService, M2M audience <c>profile-service</c>, scope <c>profiles:read</c>.</summary>
public sealed class ProfileClient(HttpClient http)
{
    public Task<ClientProfile?> GetClientAsync(Guid clientId, CancellationToken ct) =>
        RemoteCall.GetAsync<ClientProfile>(http, "ProfileService", $"api/v1/profiles/clients/{clientId}", ct);

    public Task<ClientProfile?> GetClientByUserAsync(Guid userId, CancellationToken ct) =>
        RemoteCall.GetAsync<ClientProfile>(http, "ProfileService", $"api/v1/profiles/clients/by-user/{userId}", ct);
}
