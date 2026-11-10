namespace LastMile.Contracts.Fleet;

// Topic fleet.driver.v1: owner FleetService, key = driverId (DriverProfileRejected: key = userId).
// Contract: documents/FleetService/FleetService.md §8.2.

/// <summary>A driver was created (from <c>UserRegistered</c> or by the driver's first <c>PUT /me</c>).</summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverCreated")]
public sealed record DriverCreated(Guid DriverId, Guid UserId, string FirstName, string LastName, string LicenseNumber,
    string PhoneNumber);

/// <summary>A driver's details changed. Carries the full current state.</summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverUpdated")]
public sealed record DriverUpdated(Guid DriverId, Guid UserId, string FirstName, string LastName, string LicenseNumber,
    string PhoneNumber);

/// <summary>
/// The driver's availability (decision D3: on shift and ready to deliver) changed.
/// <paramref name="VehicleId"/> is the vehicle the driver operates at that moment, if any.
/// </summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverAvailabilityChanged")]
public sealed record DriverAvailabilityChanged(Guid DriverId, Guid UserId, bool IsAvailable, Guid? VehicleId);

/// <summary>Account deactivated or role changed away from DRIVER. The driver is unavailable until reactivated.</summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverDeactivated")]
public sealed record DriverDeactivated(Guid DriverId, Guid UserId);

/// <summary>A deactivated driver is active again (still unavailable until the next shift).</summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverReactivated")]
public sealed record DriverReactivated(Guid DriverId, Guid UserId);

/// <summary>The driver's account was deleted. The row is kept (soft delete) and the vehicle released.</summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverDeleted")]
public sealed record DriverDeleted(Guid DriverId, Guid UserId);

/// <summary>
/// <c>UserRegistered</c> (DRIVER) could not become a driver, e.g. the license number is already taken.
/// No driver row exists, so the message key is the user id. The user can fix it with <c>PUT /fleet/drivers/me</c>.
/// </summary>
[IntegrationEvent(Topics.FleetDriverV1, "DriverProfileRejected")]
public sealed record DriverProfileRejected(Guid UserId, string LicenseNumber, string Reason);
