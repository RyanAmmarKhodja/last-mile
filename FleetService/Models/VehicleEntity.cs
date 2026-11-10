namespace LastMile.Fleet.Models;

/// <summary>Persistence model of the <c>vehicles</c> table.</summary>
public class VehicleEntity
{
    public Guid Id { get; set; }
    public string LicensePlate { get; set; } = null!;
    public string Model { get; set; } = null!;
    public decimal CapacityKg { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
