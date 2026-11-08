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

/// <summary>String constants for use in [Authorize(Roles = ...)] attributes.</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.ADMIN);
    public const string Driver = nameof(UserRole.DRIVER);
    public const string Client = nameof(UserRole.CLIENT);
    public const string M2M = nameof(UserRole.M2M);
}
