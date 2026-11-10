namespace LastMile.Profile.Enums;

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
    /// <summary>Read client profiles (e.g. ShipmentService validating senders and recipients).</summary>
    public const string ProfilesRead = "profiles:read";
}

/// <summary>Authorization policy names (see Program.cs).</summary>
public static class Policies
{
    /// <summary>ADMIN users or M2M tokens with <see cref="Scopes.ProfilesRead"/>.</summary>
    public const string ClientReaders = nameof(ClientReaders);

    /// <summary><see cref="ClientReaders"/> plus CLIENT users (owner check happens in the service).</summary>
    public const string ClientReadersOrOwner = nameof(ClientReadersOrOwner);
}
