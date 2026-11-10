using System.Security.Claims;
using LastMile.Fleet.Core;
using LastMile.Fleet.Enums;

namespace LastMile.Fleet.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user id (<c>sub</c>) of a human user token. M2M tokens have no user id.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(TokenClaims.Subject), out var id) && principal.HasClaim(c => c.Type == TokenClaims.Role)
            ? id
            : throw new ForbiddenException("This endpoint requires a user access token.");

    /// <summary>Who performed an action, for logs: user id or M2M client id.</summary>
    public static string ActorId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TokenClaims.ClientId) ?? principal.FindFirstValue(TokenClaims.Subject) ?? "unknown";

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Admin);

    /// <summary>True when the space-separated <c>scope</c> claim contains <paramref name="scope"/>.</summary>
    public static bool HasScope(this ClaimsPrincipal principal, string scope) =>
        principal.FindAll(TokenClaims.Scope).SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);

    /// <summary>ADMIN or a service with <c>fleet:read</c>: may read any driver or vehicle.</summary>
    public static bool CanReadFleet(this ClaimsPrincipal principal) =>
        principal.IsAdmin() || principal.HasScope(Scopes.FleetRead);
}
