using LastMile.Fleet.DTOs;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Fleet.Controllers;

/// <summary>
/// Delivery drivers: profile and availability (decision D3: on shift and ready to deliver). Drivers are created from
/// <c>UserRegistered</c> events or the driver's first <c>PUT /me</c>, and never deleted through this API.
/// </summary>
[ApiController]
[Route("api/v1/fleet/drivers")]
[Authorize]
[Produces("application/json")]
public sealed class DriversController(DriverService drivers) : ControllerBase
{
    /// <summary>The signed-in driver's own profile, availability and vehicle.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<DriverDto> GetMe(CancellationToken ct) => drivers.GetMineAsync(User, ct);

    /// <summary>Replace the signed-in driver's profile, or create it when missing (201), e.g. after a rejected license.</summary>
    [HttpPut("me")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> UpsertMe(UpdateDriverDto dto, CancellationToken ct)
    {
        var (driver, created) = await drivers.UpsertMineAsync(User, dto, ct);
        return created ? CreatedAtAction(nameof(Get), new { id = driver.Id }, driver) : Ok(driver);
    }

    /// <summary>Start (<c>true</c>) or end (<c>false</c>) a shift. Starting requires ACTIVE status and an active vehicle (409).</summary>
    [HttpPut("me/availability")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<DriverDto> SetMyAvailability(AvailabilityDto dto, CancellationToken ct) =>
        drivers.SetMyAvailabilityAsync(User, dto.IsAvailable!.Value, ct);

    /// <summary>List drivers (default: ACTIVE). Filter by availability; search matches name, license and phone. ADMIN or M2M <c>fleet:read</c>.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.FleetReaders)]
    public Task<PagedResultDto<DriverDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] DriverStatus? status = null, [FromQuery] bool? available = null,
        CancellationToken ct = default) =>
        drivers.ListAsync(page, pageSize, search, status, available, ct);

    /// <summary>A driver by id (fresh availability for dispatch). ADMIN, M2M <c>fleet:read</c>, or the driver.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.FleetReadersOrDriver)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<DriverDto> Get(Guid id, CancellationToken ct) => drivers.GetAsync(id, User, ct);

    /// <summary>A driver by AuthService user id (<c>sub</c>). ADMIN, M2M <c>fleet:read</c>, or the driver.</summary>
    [HttpGet("by-user/{userId:guid}")]
    [Authorize(Policy = Policies.FleetReadersOrDriver)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<DriverDto> GetByUser(Guid userId, CancellationToken ct) => drivers.GetByUserAsync(userId, User, ct);

    /// <summary>Replace any driver's profile. ADMIN only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<DriverDto> Update(Guid id, UpdateDriverDto dto, CancellationToken ct) => drivers.UpdateAsync(id, dto, User, ct);

    /// <summary>Set any driver's availability. ADMIN only; same rules as the driver's own toggle.</summary>
    [HttpPut("{id:guid}/availability")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<DriverDto> SetAvailability(Guid id, AvailabilityDto dto, CancellationToken ct) =>
        drivers.SetAvailabilityAsync(id, dto.IsAvailable!.Value, User, ct);
}
