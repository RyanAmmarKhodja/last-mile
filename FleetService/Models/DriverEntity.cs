namespace LastMile.Fleet.Models;

/// <summary>Persistence model of the <c>drivers</c> table.</summary>
public class DriverEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string LicenseNumber { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The vehicle this driver operates (vehicles.assigned_driver_id), if any.</summary>
    public VehicleEntity? Vehicle { get; set; }
}
