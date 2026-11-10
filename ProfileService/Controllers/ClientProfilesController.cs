using LastMile.Profile.DTOs;
using LastMile.Profile.Enums;
using LastMile.Profile.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Profile.Controllers;

/// <summary>
/// Client profiles (shipment senders and recipients). Profiles are created from <c>UserRegistered</c> events or by
/// the owner's first <c>PUT /me</c>, and are never deleted through this API: account deletion in AuthService marks them DELETED.
/// </summary>
[ApiController]
[Route("api/v1/profiles/clients")]
[Authorize]
[Produces("application/json")]
public sealed class ClientProfilesController(ClientProfileService clients) : ControllerBase
{
    /// <summary>The signed-in client's own profile.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ClientProfileDto> GetMe(CancellationToken ct) => clients.GetMineAsync(User, ct);

    /// <summary>Replace the signed-in client's profile, or create it when missing (201).</summary>
    [HttpPut("me")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> UpsertMe(UpdateClientProfileDto dto, CancellationToken ct)
    {
        var (profile, created) = await clients.UpsertMineAsync(User, dto, ct);
        return created ? CreatedAtAction(nameof(Get), new { id = profile.Id }, profile) : Ok(profile);
    }

    /// <summary>List client profiles (default: ACTIVE). Search matches names, company and phone. ADMIN or M2M <c>profiles:read</c>.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ClientReaders)]
    public Task<PagedResultDto<ClientProfileDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] ProfileStatus? status = null, CancellationToken ct = default) =>
        clients.ListAsync(page, pageSize, search, status, ct);

    /// <summary>A client profile by id. ADMIN, M2M <c>profiles:read</c>, or the owner.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ClientReadersOrOwner)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ClientProfileDto> Get(Guid id, CancellationToken ct) => clients.GetAsync(id, User, ct);

    /// <summary>A client profile by AuthService user id (<c>sub</c>). ADMIN, M2M <c>profiles:read</c>, or the owner.</summary>
    [HttpGet("by-user/{userId:guid}")]
    [Authorize(Policy = Policies.ClientReadersOrOwner)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<ClientProfileDto> GetByUser(Guid userId, CancellationToken ct) => clients.GetByUserAsync(userId, User, ct);

    /// <summary>Replace any client's profile. ADMIN only. DELETED profiles cannot be edited (409).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<ClientProfileDto> Update(Guid id, UpdateClientProfileDto dto, CancellationToken ct) =>
        clients.UpdateAsync(id, dto, User, ct);
}
