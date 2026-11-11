namespace LastMile.Contracts.Shipment;

// Topic shipment.lifecycle.v1: owner ShipmentService, key = shipmentId.
// Contract: documents/ShipmentService/ShipmentService.md §8.

/// <summary>A shipment was ordered (status CREATED). Addresses are the snapshots taken at creation.</summary>
[IntegrationEvent(Topics.ShipmentLifecycleV1, "ShipmentCreated")]
public sealed record ShipmentCreated(Guid ShipmentId, string TrackingNumber, Guid SenderId, Guid RecipientId,
    string OriginAddress, string DestinationAddress, decimal WeightKg);

/// <summary>The shipment was dispatched to a driver and vehicle (snapshots). FleetService makes the driver unavailable.</summary>
[IntegrationEvent(Topics.ShipmentLifecycleV1, "ShipmentDispatched")]
public sealed record ShipmentDispatched(Guid ShipmentId, string TrackingNumber, Guid DriverId, Guid VehicleId);

/// <summary>The driver picked the package up.</summary>
[IntegrationEvent(Topics.ShipmentLifecycleV1, "ShipmentInTransit")]
public sealed record ShipmentInTransit(Guid ShipmentId, string TrackingNumber, Guid DriverId);

/// <summary>Delivered (final). <paramref name="Notes"/> is the optional proof-of-delivery note.</summary>
[IntegrationEvent(Topics.ShipmentLifecycleV1, "ShipmentDelivered")]
public sealed record ShipmentDelivered(Guid ShipmentId, string TrackingNumber, Guid DriverId, string? Notes);

/// <summary>Delivery failed (final).</summary>
[IntegrationEvent(Topics.ShipmentLifecycleV1, "ShipmentFailed")]
public sealed record ShipmentFailed(Guid ShipmentId, string TrackingNumber, Guid DriverId, string Reason);

/// <summary>Cancelled (final). <paramref name="DriverId"/> is null when the shipment was never dispatched.</summary>
[IntegrationEvent(Topics.ShipmentLifecycleV1, "ShipmentCancelled")]
public sealed record ShipmentCancelled(Guid ShipmentId, string TrackingNumber, Guid? DriverId, string? Reason);
