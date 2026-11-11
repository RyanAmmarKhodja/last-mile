namespace LastMile.Auth.Enums;

/// <summary>
/// System roles fed into token claims and role-based authorization policies across microservices.
/// ADMIN, DRIVER and CLIENT are human user roles (stored in users.role); M2M identifies service principals.
/// </summary>
public enum UserRole
{
    ADMIN,
    DRIVER,
    CLIENT,
    M2M
}

/// <summary>OAuth scopes that AuthService itself accepts from M2M tokens.</summary>
public static class Scopes
{
    /// <summary>Resolve a user by email (<c>GET /api/v1/users/lookup</c>).</summary>
    public const string UsersLookup = "users:lookup";
}

/// <summary>Authentication scheme names (see Program.cs).</summary>
public static class AuthSchemes
{
    /// <summary>Token check for NGINX <c>auth_request</c>: every Bearer check except the audience.</summary>
    public const string Gateway = nameof(Gateway);
}

/// <summary>Authorization policy names (see Program.cs).</summary>
public static class Policies
{
    public const string UserLookup = nameof(UserLookup);
}

/// <summary>String constants for use in [Authorize(Roles = ...)] attributes.</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.ADMIN);
    public const string Driver = nameof(UserRole.DRIVER);
    public const string Client = nameof(UserRole.CLIENT);
    public const string M2M = nameof(UserRole.M2M);
}
