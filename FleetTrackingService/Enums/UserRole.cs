namespace LastMile.Tracking.Enums;

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
    /// <summary>Post pings on behalf of any driver (GPS gateway / simulator, decision T1).</summary>
    public const string TrackingIngest = "tracking:ingest";

    /// <summary>Read live positions, nearby search and shipment routes.</summary>
    public const string TrackingRead = "tracking:read";
}

/// <summary>Authorization policy names (see Program.cs).</summary>
public static class Policies
{
    /// <summary>DRIVER users (own pings) or M2M tokens with <see cref="Scopes.TrackingIngest"/>.</summary>
    public const string Ingest = nameof(Ingest);

    /// <summary>ADMIN users or M2M tokens with <see cref="Scopes.TrackingRead"/>.</summary>
    public const string TrackingReaders = nameof(TrackingReaders);

    /// <summary><see cref="TrackingReaders"/> plus DRIVER users (self check happens in the controller).</summary>
    public const string TrackingReadersOrDriver = nameof(TrackingReadersOrDriver);
}

/// <summary>Shipment statuses as published on <c>shipment.lifecycle.v1</c> (owner: ShipmentService).</summary>
public static class ShipmentStatuses
{
    public const string Created = "CREATED";
    public const string Dispatched = "DISPATCHED";
    public const string InTransit = "IN_TRANSIT";
    public const string Delivered = "DELIVERED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";

    /// <summary>The driver is carrying the shipment: its position is visible by tracking number (T3).</summary>
    public static bool IsActive(string status) => status is Dispatched or InTransit;

    public static bool IsFinal(string status) => status is Delivered or Failed or Cancelled;
}

/// <summary>Driver statuses mirrored from FleetService.</summary>
public static class DriverStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public const string Deleted = "DELETED";
}
