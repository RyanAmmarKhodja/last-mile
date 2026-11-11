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

    /// <summary>True when the space-separated <c>scope</c> claim contains <paramref name="scope"/>.</summary>
    public static bool HasScope(this ClaimsPrincipal principal, string scope) =>
        principal.FindAll(TokenClaims.Scope).SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);

    /// <summary>Who performed an action, for audit logs: user id or M2M client id.</summary>
    public static string ActorId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TokenClaims.ClientId) ?? principal.FindFirstValue(TokenClaims.Subject) ?? "unknown";
}
