using LastMile.Tracking.DTOs;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Tracking.Controllers;

/// <summary>Sampled GPS audit trail, one point per driver every 2 minutes, kept 30 days (FR-6, T5, T6).</summary>
[ApiController]
[Route("api/v1/tracking")]
[Authorize]
[Produces("application/json")]
public sealed class HistoryController(TrackingQueries queries) : ControllerBase
{
    /// <summary>
    /// A driver's track, ascending. Default range: the last 24 h; at most 7 days; <c>limit</c> ≤ 10 000.
    /// ADMIN, M2M <c>tracking:read</c>, or the driver themselves.
    /// </summary>
    [HttpGet("drivers/{driverId:guid}/history")]
    [Authorize(Policy = Policies.TrackingReadersOrDriver)]
    [ProducesResponseType<IReadOnlyList<HistoryPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IReadOnlyList<HistoryPointDto>> DriverHistory(Guid driverId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int? limit, CancellationToken ct)
    {
        await queries.EnsureCanReadDriverAsync(User, driverId, ct);
        return await queries.HistoryAsync(driverId, from, to, limit, ct);
    }

    /// <summary>The signed-in driver's own track.</summary>
    [HttpGet("drivers/me/history")]
    [Authorize(Roles = Roles.Driver)]
    public async Task<IReadOnlyList<HistoryPointDto>> MyHistory([FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int? limit, CancellationToken ct) =>
        await queries.HistoryAsync(await queries.SelfDriverIdAsync(User, ct), from, to, limit, ct);

    /// <summary>The sampled route of a shipment while it was carried. ADMIN or M2M <c>tracking:read</c>.</summary>
    [HttpGet("shipments/{shipmentId:guid}/route")]
    [Authorize(Policy = Policies.TrackingReaders)]
    [ProducesResponseType<IReadOnlyList<HistoryPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IReadOnlyList<HistoryPointDto>> ShipmentRoute(Guid shipmentId, CancellationToken ct) =>
        queries.RouteAsync(shipmentId, ct);
}
