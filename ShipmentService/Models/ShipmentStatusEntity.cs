namespace LastMile.Shipments.Models;

/// <summary>Persistence model of the append-only <c>shipment_status</c> table.</summary>
public class ShipmentStatusEntity
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
    public string ActorId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
