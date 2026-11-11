using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using LastMile.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LastMile.Auth.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth, SigningKeyService signingKeys) : ControllerBase
{
    /// <summary>
    /// Register a new CLIENT or DRIVER account. <c>profile</c> is required (fields depend on the role) and is relayed
    /// to ProfileService / FleetService in the <c>UserRegistered</c> event; AuthService does not store it.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        var user = await auth.RegisterAsync(dto, ct);
        return Created($"/api/v1/users/{user.Id}", user);
    }

    /// <summary>Authenticate with email + password; returns a short-lived access token and a refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponseDto>> Login(LoginDto dto, CancellationToken ct)
    {
        NoStore();
        return await auth.LoginAsync(dto, ct);
    }

    /// <summary>Exchange a refresh token for a new access token. The refresh token is rotated.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponseDto>> Refresh(RefreshTokenDto dto, CancellationToken ct)
    {
        NoStore();
        return await auth.RefreshAsync(dto.RefreshToken, ct);
    }

    /// <summary>Blacklist the calling access token and revoke the given refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutDto? dto, CancellationToken ct)
    {
        await auth.LogoutAsync(User, dto?.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>OAuth 2.0 client credentials grant for machine-to-machine calls.</summary>
    [HttpPost("m2m/token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<ActionResult<M2MTokenResponseDto>> M2MToken([FromForm] M2MTokenRequestDto request,
        CancellationToken ct)
    {
        NoStore();
        return await auth.IssueM2MTokenAsync(request, Request.Headers.Authorization, ct);
    }

    /// <summary>Public signing keys (JWKS) for downstream services to verify tokens locally.</summary>
    [HttpGet("jwks.json")]
    [AllowAnonymous]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public ActionResult<JwksDto> Jwks() => signingKeys.Jwks;

    /// <summary>
    /// Token validity check for the NGINX auth_request subrequest. 200 + identity headers when the
    /// token is valid and not blacklisted, 401 otherwise.
    /// </summary>
    [HttpGet("validate")]
    [Authorize(AuthenticationSchemes = AuthSchemes.Gateway)] // any service audience: the downstream service checks it
    public ActionResult<TokenIntrospectionDto> Validate()
    {
        var result = new TokenIntrospectionDto(
            User.FindFirst(TokenClaims.Subject)?.Value ?? string.Empty,
            User.FindFirst(TokenClaims.Role)?.Value,
            User.FindFirst(TokenClaims.Email)?.Value,
            User.FindFirst(TokenClaims.ClientId)?.Value,
            User.FindFirst(TokenClaims.Scope)?.Value,
            User.GetExpiry());

        Response.Headers["X-Auth-Subject"] = result.Subject;
        if (result.Role is not null) Response.Headers["X-Auth-Role"] = result.Role;
        if (result.ClientId is not null) Response.Headers["X-Auth-Client-Id"] = result.ClientId;
        return result;
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
