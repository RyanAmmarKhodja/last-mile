using System.Security.Claims;
using LastMile.Auth.Core;
using LastMile.Auth.Enums;

namespace LastMile.Auth.Services;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(TokenClaims.Subject), out var id) && principal.HasClaim(c => c.Type == TokenClaims.Role)
            ? id
            : throw new ForbiddenException("This endpoint requires a user access token.");

    public static string? GetJti(this ClaimsPrincipal principal) => principal.FindFirstValue(TokenClaims.JwtId);

    public static DateTimeOffset? GetExpiry(this ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(TokenClaims.Expires), out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Admin);
}
