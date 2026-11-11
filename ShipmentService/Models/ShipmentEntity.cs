namespace LastMile.Shipments.Models;

/// <summary>Persistence model of the <c>shipments</c> table.</summary>
public class ShipmentEntity
{
    public Guid Id { get; set; }
    public string TrackingNumber { get; set; } = null!;
    public Guid SenderId { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid RecipientId { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public Guid? AssignedDriverUserId { get; set; }
    public Guid? AssignedVehicleId { get; set; }
    public string OriginAddress { get; set; } = null!;
    public string DestinationAddress { get; set; } = null!;
    public decimal WeightKg { get; set; }
    public string CurrentStatus { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ShipmentStatusEntity> StatusHistory { get; set; } = [];
}
