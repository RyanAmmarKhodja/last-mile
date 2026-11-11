using LastMile.Tracking.DTOs;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Tracking.Controllers;

/// <summary>
/// GPS ingestion (UC-1, FR-1/FR-2). DRIVER tokens post their own pings; M2M <c>tracking:ingest</c> tokens (GPS gateway,
/// simulator) name the driver. Pings are stored only while the driver is ACTIVE and on shift (T4).
/// </summary>
[ApiController]
[Route("api/v1/tracking/telemetry")]
[Authorize(Policy = Policies.Ingest)]
[Produces("application/json")]
public sealed class TelemetryController(TelemetryService telemetry) : ControllerBase
{
    /// <summary>One ping. 204 when stored or ignored (older than the live position).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Ingest(PingDto dto, CancellationToken ct)
    {
        await telemetry.IngestAsync(dto, User, ct);
        return NoContent();
    }

    /// <summary>Up to 500 pings. Each ping is accepted, ignored or rejected on its own.</summary>
    [HttpPost("batch")]
    [ProducesResponseType<PingBatchResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PingBatchResultDto> IngestBatch(PingBatchDto dto, CancellationToken ct) =>
        telemetry.IngestBatchAsync(dto, User, ct);
}
