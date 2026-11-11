namespace LastMile.Shipments.Enums;

/// <summary>Shipment lifecycle (documents/ShipmentService/ShipmentService.md §5). The last three are final.</summary>
public enum ShipmentStatus
{
    CREATED,
    DISPATCHED,
    IN_TRANSIT,
    DELIVERED,
    FAILED,
    CANCELLED
}
