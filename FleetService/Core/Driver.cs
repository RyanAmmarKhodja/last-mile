using LastMile.Fleet.Enums;

namespace LastMile.Fleet.Core;

/// <summary>Editable details of a driver (validated by <see cref="Driver"/>).</summary>
public sealed record DriverDetails(string? FirstName, string? LastName, string? PhoneNumber, string? LicenseNumber);

/// <summary>
/// Domain model of a delivery driver: profile, status and availability (decision D3).
/// Invariant: <see cref="IsAvailable"/> implies <see cref="Status"/> = ACTIVE. The "has an active vehicle" part of the
/// rule spans two aggregates, so the caller passes it to <see cref="SetAvailable"/>.
/// </summary>
public sealed class Driver
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string LicenseNumber { get; private set; } = null!;
    public bool IsAvailable { get; private set; }
    public DriverStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Driver(Guid id, Guid userId, DriverStatus status, bool isAvailable, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        Status = status;
        IsAvailable = isAvailable;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>New drivers are ACTIVE and not available (not on shift yet).</summary>
    public static Driver Create(Guid userId, DriverDetails details)
    {
        if (userId == Guid.Empty) throw new DomainValidationException("A user id is required.");
        var now = DateTimeOffset.UtcNow;
        var driver = new Driver(Guid.CreateVersion7(), userId, DriverStatus.ACTIVE, false, now, now);
        driver.Apply(details);
        return driver;
    }

    public static Driver Rehydrate(Guid id, Guid userId, string firstName, string lastName, string phoneNumber,
        string licenseNumber, bool isAvailable, DriverStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, userId, status, isAvailable, createdAt, updatedAt)
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            LicenseNumber = licenseNumber
        };

    /// <summary>Replaces all editable fields. Returns false when nothing changed.</summary>
    public bool Update(DriverDetails details)
    {
        EnsureNotDeleted();
        var before = (FirstName, LastName, PhoneNumber, LicenseNumber);
        Apply(details);
        if (before == (FirstName, LastName, PhoneNumber, LicenseNumber)) return false;
        Touch();
        return true;
    }

    /// <summary>
    /// Start (<c>true</c>) or end (<c>false</c>) a shift. Going available requires ACTIVE status and an active vehicle.
    /// Returns false when the value does not change.
    /// </summary>
    public bool SetAvailable(bool available, bool hasActiveVehicle)
    {
        if (available == IsAvailable) return false;
        if (available)
        {
            if (Status != DriverStatus.ACTIVE)
                throw new ConflictException($"A driver with status {Status} cannot become available.");
            if (!hasActiveVehicle)
                throw new ConflictException("A driver needs an active vehicle assigned before becoming available.");
        }
        IsAvailable = available;
        Touch();
        return true;
    }

    /// <summary>Account deactivated or role changed away from DRIVER: INACTIVE and unavailable.</summary>
    public bool Deactivate()
    {
        if (Status != DriverStatus.ACTIVE) return false;
        Status = DriverStatus.INACTIVE;
        IsAvailable = false;
        Touch();
        return true;
    }

    /// <summary>Back to ACTIVE; availability stays false until the driver starts a shift.</summary>
    public bool Reactivate()
    {
        if (Status != DriverStatus.INACTIVE) return false;
        Status = DriverStatus.ACTIVE;
        Touch();
        return true;
    }

    /// <summary>Final state: the account was deleted.</summary>
    public bool MarkDeleted()
    {
        if (Status == DriverStatus.DELETED) return false;
        Status = DriverStatus.DELETED;
        IsAvailable = false;
        Touch();
        return true;
    }

    public void EnsureNotDeleted()
    {
        if (Status == DriverStatus.DELETED)
            throw new ConflictException("This driver belongs to a deleted account and cannot be changed.");
    }

    private void Apply(DriverDetails d)
    {
        FirstName = FleetRules.Required(d.FirstName, "First name", FleetRules.MaxNameLength);
        LastName = FleetRules.Required(d.LastName, "Last name", FleetRules.MaxNameLength);
        PhoneNumber = FleetRules.Required(d.PhoneNumber, "Phone number", FleetRules.MaxPhoneLength);
        LicenseNumber = FleetRules.NormalizeLicense(d.LicenseNumber);
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
