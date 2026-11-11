using System.Security.Claims;
using LastMile.Tracking.Core;
using LastMile.Tracking.Data;
using LastMile.Tracking.DTOs;
using LastMile.Tracking.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Tracking.Services;

/// <summary>Read-model and history queries (FR-4, FR-6).</summary>
public sealed class TrackingQueries(TrackingDbContext db, DriverContextCache contexts, TimeProvider clock)
{
    public const int DefaultLimit = 1000;
    public const int MaxLimit = 10_000;
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(7);

    public Task<TrackedShipmentEntity?> ResolveTrackingAsync(string trackingNumber, CancellationToken ct)
    {
        var tn = TrackingRules.NormalizeTrackingNumber(trackingNumber);
        return db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.TrackingNumber == tn, ct);
    }

    /// <summary>The caller's own driver id (DRIVER token), whether or not they are on shift.</summary>
    public async Task<Guid> SelfDriverIdAsync(ClaimsPrincipal user, CancellationToken ct) =>
        (await contexts.ByUserAsync(user.GetUserId(), ct))?.DriverId
        ?? throw new NotFoundException("No driver profile is known for this user.");

    /// <summary>ADMIN / M2M read any driver; a DRIVER only themselves (403 otherwise).</summary>
    public async Task EnsureCanReadDriverAsync(ClaimsPrincipal user, Guid driverId, CancellationToken ct)
    {
        if (user.CanReadTracking()) return;
        if (user.IsDriver() && await SelfDriverIdAsync(user, ct) == driverId) return;
        throw new ForbiddenException("You can only read your own positions.");
    }

    public async Task<IReadOnlyList<HistoryPointDto>> HistoryAsync(Guid driverId, DateTimeOffset? from,
        DateTimeOffset? to, int? limit, CancellationToken ct)
    {
        var end = (to ?? clock.GetUtcNow()).ToUniversalTime();
        var start = (from ?? end - TimeSpan.FromHours(24)).ToUniversalTime();
        if (start >= end) throw new DomainValidationException("from must be before to.");
        if (end - start > MaxRange) throw new DomainValidationException("The range can be at most 7 days.");
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        // recorded_at bounds let PostgreSQL prune to the daily partitions in range.
        return await db.History.AsNoTracking()
            .Where(h => h.DriverId == driverId && h.RecordedAt >= start && h.RecordedAt < end)
            .OrderBy(h => h.RecordedAt)
            .Take(take)
            .Select(h => HistoryPointDto.From(h))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HistoryPointDto>> RouteAsync(Guid shipmentId, CancellationToken ct)
    {
        if (!await db.Shipments.AnyAsync(s => s.ShipmentId == shipmentId, ct))
            throw new NotFoundException("Shipment not found.");
        return await db.History.AsNoTracking()
            .Where(h => h.ShipmentId == shipmentId)
            .OrderBy(h => h.RecordedAt)
            .Take(MaxLimit)
            .Select(h => HistoryPointDto.From(h))
            .ToListAsync(ct);
    }
}
