namespace LastMile.Fleet.Enums;

/// <summary>Lifecycle of a driver. Drivers are never hard-deleted (shipments keep their ids).</summary>
public enum DriverStatus
{
    /// <summary>Account active and role is DRIVER.</summary>
    ACTIVE,

    /// <summary>Account deactivated, or role changed away from DRIVER. Reversible. Never available.</summary>
    INACTIVE,

    /// <summary>Account deleted in AuthService. Final; kept for history.</summary>
    DELETED
}
