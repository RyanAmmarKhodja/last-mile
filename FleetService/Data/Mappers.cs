using LastMile.Fleet.Core;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Models;

namespace LastMile.Fleet.Data;

/// <summary>Translates between domain models (Core) and persistence models (Models).</summary>
public static class Mappers
{
    public static Driver ToDomain(this DriverEntity e) =>
        Driver.Rehydrate(e.Id, e.UserId, e.FirstName, e.LastName, e.PhoneNumber, e.LicenseNumber, e.IsAvailable,
            Enum.Parse<DriverStatus>(e.Status), e.CreatedAt, e.UpdatedAt);

    public static DriverEntity ToEntity(this Driver d)
    {
        var entity = new DriverEntity();
        d.ApplyTo(entity);
        return entity;
    }

    public static void ApplyTo(this Driver d, DriverEntity e)
    {
        e.Id = d.Id;
        e.UserId = d.UserId;
        e.FirstName = d.FirstName;
        e.LastName = d.LastName;
        e.PhoneNumber = d.PhoneNumber;
        e.LicenseNumber = d.LicenseNumber;
        e.IsAvailable = d.IsAvailable;
        e.Status = d.Status.ToString();
        e.CreatedAt = d.CreatedAt;
        e.UpdatedAt = d.UpdatedAt;
    }

    public static Vehicle ToDomain(this VehicleEntity e) =>
        Vehicle.Rehydrate(e.Id, e.LicensePlate, e.Model, e.CapacityKg, e.AssignedDriverId, e.IsActive, e.CreatedAt, e.UpdatedAt);

    public static VehicleEntity ToEntity(this Vehicle v)
    {
        var entity = new VehicleEntity();
        v.ApplyTo(entity);
        return entity;
    }

    public static void ApplyTo(this Vehicle v, VehicleEntity e)
    {
        e.Id = v.Id;
        e.LicensePlate = v.LicensePlate;
        e.Model = v.Model;
        e.CapacityKg = v.CapacityKg;
        e.AssignedDriverId = v.AssignedDriverId;
        e.IsActive = v.IsActive;
        e.CreatedAt = v.CreatedAt;
        e.UpdatedAt = v.UpdatedAt;
    }
}
