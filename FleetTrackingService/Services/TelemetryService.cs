using System.Security.Claims;
using LastMile.Tracking.Core;
using LastMile.Tracking.DTOs;
using LastMile.Tracking.Options;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Services;

/// <summary>
/// Ping ingestion (FR-1, FR-2, §4.2): resolve the driver from the token (T1), require ACTIVE + on shift (T4),
/// validate, then one Redis round trip. No database write per ping.
/// </summary>
public sealed class TelemetryService(
    DriverContextCache contexts,
    LivePositionStore live,
    IOptions<TrackingOptions> options,
    TimeProvider clock)
{
    /// <summary>Returns true when stored, false when ignored (older than the live ping). Throws on rejection.</summary>
    public async Task<bool> IngestAsync(PingDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ping = Validate(dto, now);
        var ctx = TrackingRules.RequireOnShift(await ResolveAsync(dto.DriverId, actor, ct));
        return await live.WriteAsync(ctx, ping, now);
    }

    public async Task<PingBatchResultDto> IngestBatchAsync(PingBatchDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var pings = dto.Pings ?? [];
        if (pings.Count == 0) throw new DomainValidationException("pings must contain at least one ping.");
        if (pings.Count > options.Value.MaxBatchSize)
            throw new DomainValidationException($"A batch holds at most {options.Value.MaxBatchSize} pings.");

        // A DRIVER batch is their own buffered pings: resolve once and refuse foreign driver ids up front.
        DriverContext? self = null;
        if (!actor.CanIngestForAnyDriver())
        {
            self = await ResolveAsync(null, actor, ct);
            if (pings.Any(p => p.DriverId is { } id && id != self?.DriverId))
                throw new ForbiddenException("A driver can only post their own pings.");
        }

        var now = clock.GetUtcNow();
        var rejected = new List<RejectedPingDto>();
        var writes = new List<Task<bool>>(pings.Count);
        for (var i = 0; i < pings.Count; i++)
        {
            try
            {
                var ping = Validate(pings[i], now);
                var ctx = self ?? (pings[i].DriverId is { } id
                    ? await contexts.ByDriverAsync(id, ct)
                    : throw new DomainValidationException("driverId is required for M2M pings."));
                writes.Add(live.WriteAsync(TrackingRules.RequireOnShift(ctx), ping, now)); // pipelined by the multiplexer
            }
            catch (DomainException ex)
            {
                rejected.Add(new RejectedPingDto(i, ex.Message));
            }
        }

        var results = await Task.WhenAll(writes);
        var accepted = results.Count(r => r);
        return new PingBatchResultDto(accepted, results.Length - accepted, rejected);
    }

    private Ping Validate(PingDto dto, DateTimeOffset now) => TrackingRules.Validate(dto.Latitude, dto.Longitude,
        dto.Timestamp, dto.SpeedKmh, dto.HeadingDeg, dto.AccuracyM, now, options.Value.MaxFutureSkew, options.Value.MaxPingAge);

    /// <summary>T1: DRIVER → the driver mapped from <c>sub</c>; M2M <c>tracking:ingest</c> → the named driver.</summary>
    private async Task<DriverContext?> ResolveAsync(Guid? driverId, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (actor.CanIngestForAnyDriver())
            return driverId is { } id
                ? await contexts.ByDriverAsync(id, ct)
                : throw new DomainValidationException("driverId is required for M2M pings.");

        if (!actor.IsDriver()) throw new ForbiddenException("Only drivers or a tracking:ingest client can post pings.");
        var ctx = await contexts.ByUserAsync(actor.GetUserId(), ct);
        if (driverId is { } claimed && claimed != ctx?.DriverId)
            throw new ForbiddenException("A driver can only post their own pings.");
        return ctx;
    }
}
