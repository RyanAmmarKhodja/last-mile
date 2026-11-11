namespace LastMile.Shipments.Enums;

/// <summary>Role names carried in the <c>role</c> claim of user access tokens issued by AuthService.</summary>
public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Driver = "DRIVER";
    public const string Client = "CLIENT";
}

/// <summary>OAuth scopes. <see cref="ShipmentsRead"/> is accepted here; the others are requested from downstream services.</summary>
public static class Scopes
{
    /// <summary>Read all shipments (future FleetTrackingService / NotificationService).</summary>
    public const string ShipmentsRead = "shipments:read";

    public const string UsersLookup = "users:lookup";
    public const string ProfilesRead = "profiles:read";
    public const string FleetRead = "fleet:read";
}

/// <summary>Audiences of the downstream services (M2M tokens are requested per audience).</summary>
public static class Audiences
{
    public const string Auth = "auth-service";
    public const string Profile = "profile-service";
    public const string Fleet = "fleet-service";
}
