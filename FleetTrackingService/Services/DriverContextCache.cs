using LastMile.Tracking.Core;
using LastMile.Tracking.Data;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Services;

/// <summary>
/// Per-replica cache of <see cref="DriverContext"/> (read model: driver + active shipment), so a ping costs no database
/// round trip (NFR-1). Entries live <see cref="TrackingOptions.ContextCacheSeconds"/>; misses are cached too, so unknown
/// drivers cannot hammer PostgreSQL. The consumer invalidates entries on this replica as events arrive.
/// </summary>
public sealed class DriverContextCache(IServiceScopeFactory scopes, IMemoryCache cache, IOptions<TrackingOptions> options)
{
    private TimeSpan Ttl => TimeSpan.FromSeconds(Math.Max(1, options.Value.ContextCacheSeconds));

    public Task<DriverContext?> ByUserAsync(Guid userId, CancellationToken ct) =>
        GetAsync($"u:{userId}", db => db.Drivers.AsNoTracking().Where(d => d.UserId == userId), ct);

    public Task<DriverContext?> ByDriverAsync(Guid driverId, CancellationToken ct) =>
        GetAsync($"d:{driverId}", db => db.Drivers.AsNoTracking().Where(d => d.DriverId == driverId), ct);

    public void Invalidate(Guid driverId, Guid? userId = null)
    {
        cache.Remove($"d:{driverId}");
        if (userId is { } u) cache.Remove($"u:{u}");
    }

    private async Task<DriverContext?> GetAsync(string key,
        Func<TrackingDbContext, IQueryable<Models.TrackedDriverEntity>> query, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out DriverContext? hit)) return hit;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var ctx = await query(db)
            .Select(d => new
            {
                Driver = d,
                Shipment = db.Shipments
                    .Where(s => s.DriverId == d.DriverId &&
                                (s.Status == ShipmentStatuses.Dispatched || s.Status == ShipmentStatuses.InTransit))
                    .OrderByDescending(s => s.UpdatedAt)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        DriverContext? result = ctx is null
            ? null
            : new DriverContext(ctx.Driver.DriverId, ctx.Driver.UserId, ctx.Driver.Status, ctx.Driver.IsAvailable,
                ctx.Driver.VehicleId, ctx.Shipment?.ShipmentId, ctx.Shipment?.VehicleId, ctx.Shipment?.TrackingNumber);
        cache.Set(key, result, Ttl);
        return result;
    }
}
