using LastMile.Fleet.DTOs;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Fleet.Controllers;

/// <summary>Delivery vehicles and the driver ↔ vehicle assignment. Vehicles are retired, never deleted.</summary>
[ApiController]
[Route("api/v1/fleet/vehicles")]
[Authorize]
[Produces("application/json")]
public sealed class VehiclesController(VehicleService vehicles) : ControllerBase
{
    /// <summary>List vehicles (default: active). Filter by assignment; search matches plate and model. ADMIN or M2M <c>fleet:read</c>.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.FleetReaders)]
    public Task<PagedResultDto<VehicleDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? active = null, [FromQuery] bool? assigned = null,
        CancellationToken ct = default) =>
        vehicles.ListAsync(page, pageSize, search, active, assigned, ct);

    /// <summary>A vehicle by id. ADMIN, M2M <c>fleet:read</c>, or the driver it is assigned to.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.FleetReadersOrDriver)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<VehicleDto> Get(Guid id, CancellationToken ct) => vehicles.GetAsync(id, User, ct);

    /// <summary>Register a vehicle. ADMIN only. Plates are unique (case-insensitive).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(VehicleDetailsDto dto, CancellationToken ct)
    {
        var vehicle = await vehicles.CreateAsync(dto, User, ct);
        return CreatedAtAction(nameof(Get), new { id = vehicle.Id }, vehicle);
    }

    /// <summary>Replace a vehicle's details. ADMIN only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<VehicleDto> Update(Guid id, VehicleDetailsDto dto, CancellationToken ct) => vehicles.UpdateAsync(id, dto, User, ct);

    /// <summary>
    /// Assign the vehicle to an ACTIVE driver without another vehicle, or unassign it (<c>driverId: null</c>).
    /// The previous driver, if any, becomes unavailable. ADMIN only.
    /// </summary>
    [HttpPut("{id:guid}/assignment")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<VehicleDto> Assign(Guid id, AssignmentDto dto, CancellationToken ct) =>
        vehicles.AssignAsync(id, dto.DriverId, User, ct);

    /// <summary>Retire (<c>isActive: false</c>, unassigns it first) or reactivate a vehicle. ADMIN only.</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<VehicleDto> SetStatus(Guid id, VehicleStatusDto dto, CancellationToken ct) =>
        vehicles.SetActiveAsync(id, dto.IsActive!.Value, User, ct);
}
