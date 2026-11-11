namespace LastMile.Tracking.Models;

/// <summary>Read model of a FleetService driver, fed by <c>fleet.driver.v1</c>. Written only by the consumer.</summary>
public sealed class TrackedDriverEntity
{
    public Guid DriverId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public bool IsAvailable { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Read model of a ShipmentService shipment, fed by <c>shipment.lifecycle.v1</c>. Written only by the consumer.</summary>
public sealed class TrackedShipmentEntity
{
    public Guid ShipmentId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "CREATED";
    public Guid? DriverId { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One sampled position (T6). The table is partitioned by day on <see cref="RecordedAt"/> and created with raw SQL
/// (see the InitialCreate migration); the generated PostGIS <c>position</c> column is not mapped.
/// </summary>
public sealed class LocationHistoryEntity
{
    public Guid DriverId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? ShipmentId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float? SpeedKmh { get; set; }
    public float? HeadingDeg { get; set; }
    public float? AccuracyM { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
