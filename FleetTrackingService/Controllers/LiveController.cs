using LastMile.Tracking.Core;
using LastMile.Tracking.DTOs;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Options;
using LastMile.Tracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Controllers;

/// <summary>Live positions for the admin fleet map, services and drivers themselves (FR-4, T3).</summary>
[ApiController]
[Route("api/v1/tracking")]
[Authorize]
[Produces("application/json")]
public sealed class LiveController(
    LivePositionStore live,
    PositionHub hub,
    TrackingQueries queries,
    IOptions<TrackingOptions> options) : ControllerBase
{
    /// <summary>Every live driver. ADMIN or M2M <c>tracking:read</c>.</summary>
    [HttpGet("live")]
    [Authorize(Policy = Policies.TrackingReaders)]
    public async Task<IReadOnlyList<LivePositionDto>> Fleet() =>
        (await live.GetAllAsync()).Select(LivePositionDto.From).ToList();

    /// <summary>Live drivers within <paramref name="radiusKm"/> of a point, closest first. ADMIN or M2M <c>tracking:read</c>.</summary>
    [HttpGet("live/nearby")]
    [Authorize(Policy = Policies.TrackingReaders)]
    [ProducesResponseType<IReadOnlyList<NearbyPositionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IReadOnlyList<NearbyPositionDto>> Nearby([FromQuery] double? lat, [FromQuery] double? lon,
        [FromQuery] double radiusKm = 5, [FromQuery] int limit = 20)
    {
        if (lat is not { } la || la is < -TrackingRules.MaxLatitude or > TrackingRules.MaxLatitude)
            throw new DomainValidationException("lat must be between -85.05 and 85.05.");
        if (lon is not { } lo || lo is < -180 or > 180)
            throw new DomainValidationException("lon must be between -180 and 180.");
        if (radiusKm is <= 0 or > 500) throw new DomainValidationException("radiusKm must be in (0, 500].");
        var hits = await live.NearbyAsync(la, lo, radiusKm, Math.Clamp(limit, 1, 200));
        return hits.Select(h => new NearbyPositionDto(h.DistanceKm, LivePositionDto.From(h.Position))).ToList();
    }

    /// <summary>
    /// SSE fleet stream for the admin map: a <c>positions</c> snapshot, then one coalesced <c>positions</c> batch per
    /// second (latest per driver), <c>offline</c> when drivers leave the map, and <c>heartbeat</c> when idle.
    /// </summary>
    [HttpGet("live/stream")]
    [Authorize(Roles = Roles.Admin)]
    [Produces("text/event-stream")]
    public async Task FleetStream(CancellationToken ct)
    {
        using var sink = hub.SubscribeFleet();
        var interval = options.Value.FleetStreamInterval;
        var heartbeat = options.Value.HeartbeatInterval;
        try
        {
            await SseWriter.StartAsync(Response, ct);
            await SseWriter.WriteAsync(Response, "positions",
                (await live.GetAllAsync()).Select(LivePositionDto.From).ToList(), ct);

            var idle = TimeSpan.Zero;
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                var batch = sink.Drain();
                var positions = batch.Where(m => m is { Type: "position", Position: not null })
                    .Select(m => LivePositionDto.From(m.Position!)).ToList();
                var offline = batch.Where(m => m.Type == "offline").Select(m => m.DriverId).ToList();

                if (positions.Count > 0) await SseWriter.WriteAsync(Response, "positions", positions, ct);
                if (offline.Count > 0) await SseWriter.WriteAsync(Response, "offline", new { driverIds = offline }, ct);

                idle = positions.Count + offline.Count > 0 ? TimeSpan.Zero : idle + interval;
                if (idle >= heartbeat)
                {
                    await SseWriter.WriteAsync(Response, "heartbeat", new { }, ct);
                    idle = TimeSpan.Zero;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // client went away
        }
    }

    /// <summary>One driver's live position. ADMIN, M2M <c>tracking:read</c>, or the driver themselves.</summary>
    [HttpGet("drivers/{driverId:guid}/live")]
    [Authorize(Policy = Policies.TrackingReadersOrDriver)]
    [ProducesResponseType<LivePositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<LivePositionDto> Driver(Guid driverId, CancellationToken ct)
    {
        await queries.EnsureCanReadDriverAsync(User, driverId, ct);
        return await LiveOrNotFound(driverId);
    }

    /// <summary>The signed-in driver's live position.</summary>
    [HttpGet("drivers/me/live")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<LivePositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<LivePositionDto> Me(CancellationToken ct) =>
        await LiveOrNotFound(await queries.SelfDriverIdAsync(User, ct));

    private async Task<LivePositionDto> LiveOrNotFound(Guid driverId) =>
        await live.GetAsync(driverId) is { } p
            ? LivePositionDto.From(p)
            : throw new NotFoundException("This driver has no live position (off shift or no ping in the last 10 minutes).");
}
