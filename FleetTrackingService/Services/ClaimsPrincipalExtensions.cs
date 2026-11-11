using System.Security.Claims;
using LastMile.Tracking.Core;
using LastMile.Tracking.Enums;

namespace LastMile.Tracking.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user id (<c>sub</c>) of a human user token. M2M tokens have no user id.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(TokenClaims.Subject), out var id) && principal.HasClaim(c => c.Type == TokenClaims.Role)
            ? id
            : throw new ForbiddenException("This endpoint requires a user access token.");

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Admin);
    public static bool IsDriver(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Driver);

    /// <summary>True when the space-separated <c>scope</c> claim contains <paramref name="scope"/>.</summary>
    public static bool HasScope(this ClaimsPrincipal principal, string scope) =>
        principal.FindAll(TokenClaims.Scope).SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);

    /// <summary>M2M GPS gateway / simulator: may post pings for any driver (T1).</summary>
    public static bool CanIngestForAnyDriver(this ClaimsPrincipal principal) => principal.HasScope(Scopes.TrackingIngest);

    /// <summary>ADMIN or a service with <c>tracking:read</c>.</summary>
    public static bool CanReadTracking(this ClaimsPrincipal principal) =>
        principal.IsAdmin() || principal.HasScope(Scopes.TrackingRead);
}
