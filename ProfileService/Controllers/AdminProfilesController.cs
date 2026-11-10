using LastMile.Profile.DTOs;
using LastMile.Profile.Enums;
using LastMile.Profile.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Profile.Controllers;

/// <summary>Administrator profiles. ADMIN only.</summary>
[ApiController]
[Route("api/v1/profiles/admins")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class AdminProfilesController(AdminProfileService admins) : ControllerBase
{
    /// <summary>The signed-in administrator's own profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType<AdminProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<AdminProfileDto> GetMe(CancellationToken ct) => admins.GetMineAsync(User, ct);

    /// <summary>Replace the signed-in administrator's profile, or create it when missing (201).</summary>
    [HttpPut("me")]
    [ProducesResponseType<AdminProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AdminProfileDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> UpsertMe(UpdateAdminProfileDto dto, CancellationToken ct)
    {
        var (profile, created) = await admins.UpsertMineAsync(User, dto, ct);
        return created ? CreatedAtAction(nameof(Get), new { id = profile.Id }, profile) : Ok(profile);
    }

    /// <summary>List administrator profiles (default: ACTIVE). Search matches names and phone.</summary>
    [HttpGet]
    public Task<PagedResultDto<AdminProfileDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] ProfileStatus? status = null, CancellationToken ct = default) =>
        admins.ListAsync(page, pageSize, search, status, ct);

    /// <summary>An administrator profile by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<AdminProfileDto> Get(Guid id, CancellationToken ct) => admins.GetAsync(id, ct);

    /// <summary>Replace any administrator's profile. DELETED profiles cannot be edited (409).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<AdminProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public Task<AdminProfileDto> Update(Guid id, UpdateAdminProfileDto dto, CancellationToken ct) =>
        admins.UpdateAsync(id, dto, User, ct);
}
