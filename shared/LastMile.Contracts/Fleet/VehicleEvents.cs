namespace LastMile.Contracts.Fleet;

// Topic fleet.vehicle.v1: owner FleetService, key = vehicleId.
// Contract: documents/FleetService/FleetService.md §8.2.

/// <summary>A vehicle was registered.</summary>
[IntegrationEvent(Topics.FleetVehicleV1, "VehicleCreated")]
public sealed record VehicleCreated(Guid VehicleId, string LicensePlate, string Model, decimal CapacityKg);

/// <summary>A vehicle's details changed. Carries the full current state.</summary>
[IntegrationEvent(Topics.FleetVehicleV1, "VehicleUpdated")]
public sealed record VehicleUpdated(Guid VehicleId, string LicensePlate, string Model, decimal CapacityKg);

/// <summary>The vehicle's operator changed. <paramref name="DriverId"/> null = unassigned.</summary>
[IntegrationEvent(Topics.FleetVehicleV1, "VehicleAssigned")]
public sealed record VehicleAssigned(Guid VehicleId, Guid? DriverId, Guid? PreviousDriverId);

/// <summary>The vehicle was retired (cannot be assigned).</summary>
[IntegrationEvent(Topics.FleetVehicleV1, "VehicleDeactivated")]
public sealed record VehicleDeactivated(Guid VehicleId);

/// <summary>A retired vehicle is back in service.</summary>
[IntegrationEvent(Topics.FleetVehicleV1, "VehicleReactivated")]
public sealed record VehicleReactivated(Guid VehicleId);
