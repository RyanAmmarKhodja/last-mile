using LastMile.Shipments.DTOs;
using LastMile.Shipments.Enums;
using LastMile.Shipments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Shipments.Controllers;

/// <summary>
/// Shipment lifecycle. Shipments are never edited or deleted: mistakes are cancelled and recreated, keeping the
/// audit trail honest. Transitions are POST actions.
/// </summary>
[ApiController]
[Route("api/v1/shipments")]
[Authorize]
[Produces("application/json")]
public sealed class ShipmentsController(ShipmentService shipments, DispatchService dispatch) : ControllerBase
{
    /// <summary>
    /// Create a shipment (decision S1). CLIENT: you are the sender; name the recipient by <c>recipientEmail</c>.
    /// ADMIN: give <c>senderClientId</c> and <c>recipientClientId</c> or <c>recipientEmail</c>.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Create(CreateShipmentDto dto, CancellationToken ct)
    {
        var shipment = await shipments.CreateAsync(dto, User, ct);
        return CreatedAtAction(nameof(Get), new { id = shipment.Id }, shipment);
    }

    /// <summary>
    /// List shipments, newest first. ADMIN / M2M <c>shipments:read</c>: all (filter by driverId). CLIENT: those you send or
    /// receive (<c>as=sender|recipient</c>). DRIVER: those assigned to you. <c>search</c> matches the tracking number.
    /// </summary>
    [HttpGet]
    public Task<PagedResultDto<ShipmentSummaryDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] ShipmentStatus? status = null, [FromQuery(Name = "as")] string? asParty = null,
        [FromQuery] Guid? driverId = null, [FromQuery] string? search = null, CancellationToken ct = default) =>
        shipments.ListAsync(page, pageSize, status, asParty, driverId, search, User, ct);

    /// <summary>A shipment with its full history. ADMIN, M2M, sender, recipient or assigned driver.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ShipmentDto> Get(Guid id, CancellationToken ct) => shipments.GetAsync(id, User, ct);

    /// <summary>
    /// Dispatch a CREATED shipment (decision S2). With <c>driverId</c>: that driver must be ACTIVE, available, with a vehicle
    /// that fits and no other active shipment. Without: auto-pick the smallest fitting vehicle. ADMIN only.
    /// </summary>
    [HttpPost("{id:guid}/dispatch")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public Task<ShipmentDto> Dispatch(Guid id, [FromBody] DispatchDto? dto, CancellationToken ct) =>
        dispatch.DispatchAsync(id, dto?.DriverId, User, ct);

    /// <summary>The package was picked up: DISPATCHED → IN_TRANSIT. Assigned driver or ADMIN.</summary>
    [HttpPost("{id:guid}/pickup")]
    [Authorize(Roles = $"{Roles.Driver},{Roles.Admin}")]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<ShipmentDto> Pickup(Guid id, CancellationToken ct) => shipments.PickupAsync(id, User, ct);

    /// <summary>Delivered (proof-of-delivery note optional): IN_TRANSIT → DELIVERED. Assigned driver or ADMIN.</summary>
    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = $"{Roles.Driver},{Roles.Admin}")]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<ShipmentDto> Deliver(Guid id, [FromBody] NoteDto? dto, CancellationToken ct) =>
        shipments.DeliverAsync(id, dto?.Notes, User, ct);

    /// <summary>Delivery failed (reason required): IN_TRANSIT → FAILED. Assigned driver or ADMIN.</summary>
    [HttpPost("{id:guid}/fail")]
    [Authorize(Roles = $"{Roles.Driver},{Roles.Admin}")]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<ShipmentDto> Fail(Guid id, ReasonDto dto, CancellationToken ct) => shipments.FailAsync(id, dto.Reason, User, ct);

    /// <summary>Cancel. ADMIN: any non-final shipment. Sender: only before dispatch.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    [ProducesResponseType<ShipmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<ShipmentDto> Cancel(Guid id, [FromBody] NoteDto? dto, CancellationToken ct) =>
        shipments.CancelAsync(id, dto?.Notes, User, ct);
}
