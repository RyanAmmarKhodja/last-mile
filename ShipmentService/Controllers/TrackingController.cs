using LastMile.Shipments.DTOs;
using LastMile.Shipments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Shipments.Controllers;

/// <summary>
/// Public tracking link (decision S3, PRD persona "Customer / Recipient"). No login; NGINX rate-limits this route.
/// Returns status and timeline only, never names, addresses, phones or notes.
/// </summary>
[ApiController]
[Route("api/v1/shipments/track")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class TrackingController(ShipmentService shipments) : ControllerBase
{
    /// <summary>Status, timeline and driver/vehicle ids (for the live map) of a shipment, by tracking number.</summary>
    [HttpGet("{trackingNumber}")]
    [ProducesResponseType<TrackingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<TrackingDto> Track(string trackingNumber, CancellationToken ct) => shipments.TrackAsync(trackingNumber, ct);
}
