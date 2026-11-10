using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using LastMile.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Auth.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(UserService users) : ControllerBase
{
    /// <summary>Profile details of the currently logged-in user.</summary>
    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => users.GetAsync(User.GetUserId(), User, ct);

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public Task<PagedResultDto<UserDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] UserRole? role = null, [FromQuery] string? search = null, CancellationToken ct = default) =>
        users.ListAsync(page, pageSize, role, search, ct);

    [HttpGet("{id:guid}")]
    public Task<UserDto> Get(Guid id, CancellationToken ct) => users.GetAsync(id, User, ct);

    /// <summary>
    /// Create an account of any role (including ADMIN). <c>profile</c> is required, as for self-registration, and is
    /// relayed in <c>UserRegistered</c>.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken ct)
    {
        var user = await users.CreateAsync(dto, User, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    /// <summary>Update an account. Owners may change email/password (with currentPassword); admins may change anything.</summary>
    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserDto dto, CancellationToken ct) => users.UpdateAsync(id, dto, User, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await users.DeleteAsync(id, User, ct);
        return NoContent();
    }
}
