namespace LastMile.Shipments.Services.Clients;

/// <summary>FleetService, M2M audience <c>fleet-service</c>, scope <c>fleet:read</c>. Reads are uncached (fresh availability).</summary>
public sealed class FleetClient(HttpClient http)
{
    public Task<FleetDriver?> GetDriverAsync(Guid driverId, CancellationToken ct) =>
        RemoteCall.GetAsync<FleetDriver>(http, "FleetService", $"api/v1/fleet/drivers/{driverId}", ct);

    /// <summary>ACTIVE drivers that are currently available (first 100; enough for the MVP fleet).</summary>
    public async Task<IReadOnlyList<FleetDriver>> ListAvailableDriversAsync(CancellationToken ct) =>
        (await RemoteCall.GetAsync<RemotePage<FleetDriver>>(http, "FleetService",
            "api/v1/fleet/drivers?available=true&pageSize=100", ct))?.Items ?? [];
}
