namespace LastMile.Profile.Enums;

/// <summary>Lifecycle of a profile. Profiles are never hard-deleted (shipments keep referencing clients).</summary>
public enum ProfileStatus
{
    /// <summary>Account active and role matches the profile.</summary>
    ACTIVE,

    /// <summary>Account deactivated, or role changed away from this profile's role. Reversible.</summary>
    INACTIVE,

    /// <summary>Account deleted in AuthService. Final; kept for history.</summary>
    DELETED
}
