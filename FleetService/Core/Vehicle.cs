namespace LastMile.Fleet.Core;

/// <summary>Editable details of a vehicle (validated by <see cref="Vehicle"/>).</summary>
public sealed record VehicleDetails(string? LicensePlate, string? Model, decimal? CapacityKg);

/// <summary>
/// Domain model of a delivery vehicle. Invariant: a retired vehicle (<see cref="IsActive"/> = false) has no driver.
/// One-vehicle-per-driver is enforced by the service (and a unique index).
/// </summary>
public sealed class Vehicle
{
    public Guid Id { get; }
    public string LicensePlate { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public decimal CapacityKg { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Vehicle(Guid id, Guid? assignedDriverId, bool isActive, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        AssignedDriverId = assignedDriverId;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Vehicle Create(VehicleDetails details)
    {
        var now = DateTimeOffset.UtcNow;
        var vehicle = new Vehicle(Guid.CreateVersion7(), null, true, now, now);
        vehicle.Apply(details);
        return vehicle;
    }

    public static Vehicle Rehydrate(Guid id, string licensePlate, string model, decimal capacityKg, Guid? assignedDriverId,
        bool isActive, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, assignedDriverId, isActive, createdAt, updatedAt)
        {
            LicensePlate = licensePlate,
            Model = model,
            CapacityKg = capacityKg
        };

    /// <summary>Replaces all editable fields. Returns false when nothing changed.</summary>
    public bool Update(VehicleDetails details)
    {
        var before = (LicensePlate, Model, CapacityKg);
        Apply(details);
        if (before == (LicensePlate, Model, CapacityKg)) return false;
        Touch();
        return true;
    }

    /// <summary>Sets or clears the operator. Returns false when unchanged.</summary>
    public bool AssignTo(Guid? driverId)
    {
        if (driverId == AssignedDriverId) return false;
        if (driverId is not null && !IsActive)
            throw new ConflictException("A retired vehicle cannot be assigned. Reactivate it first.");
        AssignedDriverId = driverId;
        Touch();
        return true;
    }

    /// <summary>Retire the vehicle. It must be unassigned first (the service does it and handles the driver).</summary>
    public bool Retire()
    {
        if (!IsActive) return false;
        if (AssignedDriverId is not null)
            throw new ConflictException("Unassign the vehicle before retiring it.");
        IsActive = false;
        Touch();
        return true;
    }

    public bool Reactivate()
    {
        if (IsActive) return false;
        IsActive = true;
        Touch();
        return true;
    }

    private void Apply(VehicleDetails d)
    {
        LicensePlate = FleetRules.NormalizePlate(d.LicensePlate);
        Model = FleetRules.Required(d.Model, "Model", FleetRules.MaxModelLength);
        CapacityKg = FleetRules.Capacity(d.CapacityKg);
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
