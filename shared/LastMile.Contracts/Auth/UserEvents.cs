namespace LastMile.Contracts.Auth;

// Topic auth.user.v1: owner AuthService, key = userId.
// Contract: documents/data-ownership-matrix.md §6 and §8.1, documents/kafka-architecture.md §3.

/// <summary>Human roles. <c>M2M</c> is never a user role, so it is not part of the contract.</summary>
public enum UserRole
{
    ADMIN,
    DRIVER,
    CLIENT
}

/// <summary>
/// Profile fields collected at registration and relayed to the owning service (decision D2).
/// AuthService does not store them. ProfileService stores ADMIN/CLIENT profiles and FleetService stores DRIVER profiles.
/// </summary>
/// <param name="PhoneNumber">Required for CLIENT and DRIVER, optional for ADMIN.</param>
/// <param name="CompanyName">CLIENT only, optional.</param>
/// <param name="Address">CLIENT only, required.</param>
/// <param name="LicenseNumber">DRIVER only, required. Uniqueness is checked by FleetService.</param>
public sealed record RegistrationProfile(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? CompanyName,
    string? Address,
    string? LicenseNumber);

/// <summary>A user account was created (self-registration, admin creation or seeding).</summary>
[IntegrationEvent(Topics.AuthUserV1, "UserRegistered")]
public sealed record UserRegistered(Guid UserId, string Email, UserRole Role, RegistrationProfile Profile);

/// <summary>The account's (normalized) email changed.</summary>
[IntegrationEvent(Topics.AuthUserV1, "UserEmailChanged")]
public sealed record UserEmailChanged(Guid UserId, string Email);

/// <summary>An administrator changed the account's role.</summary>
[IntegrationEvent(Topics.AuthUserV1, "UserRoleChanged")]
public sealed record UserRoleChanged(Guid UserId, UserRole PreviousRole, UserRole Role);

/// <summary>The account can no longer log in (<c>users.is_active = false</c>).</summary>
[IntegrationEvent(Topics.AuthUserV1, "UserDeactivated")]
public sealed record UserDeactivated(Guid UserId, UserRole Role);

/// <summary>A deactivated account can log in again (<c>users.is_active = true</c>).</summary>
[IntegrationEvent(Topics.AuthUserV1, "UserReactivated")]
public sealed record UserReactivated(Guid UserId, UserRole Role);

/// <summary>The account was deleted from auth_db. Consumers soft-delete their profile.</summary>
[IntegrationEvent(Topics.AuthUserV1, "UserDeleted")]
public sealed record UserDeleted(Guid UserId, UserRole Role);
