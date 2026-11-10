namespace LastMile.Fleet.Enums;

/// <summary>Role names carried in the <c>role</c> claim of user access tokens issued by AuthService.</summary>
public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Driver = "DRIVER";
    public const string Client = "CLIENT";
}

/// <summary>OAuth scopes accepted from M2M (client-credentials) tokens.</summary>
public static class Scopes
{
    /// <summary>Read drivers and vehicles (e.g. ShipmentService checking availability before dispatch).</summary>
    public const string FleetRead = "fleet:read";
}

/// <summary>Authorization policy names (see Program.cs).</summary>
public static class Policies
{
    /// <summary>ADMIN users or M2M tokens with <see cref="Scopes.FleetRead"/>.</summary>
    public const string FleetReaders = nameof(FleetReaders);

    /// <summary><see cref="FleetReaders"/> plus DRIVER users (owner / assigned-driver check happens in the service).</summary>
    public const string FleetReadersOrDriver = nameof(FleetReadersOrDriver);
}
