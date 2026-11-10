using LastMile.Contracts.Fleet;
using LastMile.Fleet.Core;
using LastMile.Fleet.Enums;
using LastMile.Messaging.Outbox;

namespace LastMile.Fleet.Services;

/// <summary>
/// Stages <c>fleet.driver.v1</c> and <c>fleet.vehicle.v1</c> events in the outbox. Call it <b>before</b> the
/// <c>SaveChanges</c> that persists the change, so the event and the change commit atomically.
/// </summary>
public sealed class FleetEventPublisher(IEventOutbox outbox)
{
    // ---- fleet.driver.v1 (key = driverId) ------------------------------------------------------
    public void DriverCreated(Driver d) => outbox.Enqueue(d.Id.ToString(),
        new DriverCreated(d.Id, d.UserId, d.FirstName, d.LastName, d.LicenseNumber, d.PhoneNumber));

    public void DriverUpdated(Driver d) => outbox.Enqueue(d.Id.ToString(),
        new DriverUpdated(d.Id, d.UserId, d.FirstName, d.LastName, d.LicenseNumber, d.PhoneNumber));

    public void AvailabilityChanged(Driver d, Guid? vehicleId) => outbox.Enqueue(d.Id.ToString(),
        new DriverAvailabilityChanged(d.Id, d.UserId, d.IsAvailable, vehicleId));

    /// <summary>Publishes the event matching the driver's new status.</summary>
    public void DriverStatusChanged(Driver d)
    {
        var key = d.Id.ToString();
        switch (d.Status)
        {
            case DriverStatus.ACTIVE: outbox.Enqueue(key, new DriverReactivated(d.Id, d.UserId)); break;
            case DriverStatus.INACTIVE: outbox.Enqueue(key, new DriverDeactivated(d.Id, d.UserId)); break;
            case DriverStatus.DELETED: outbox.Enqueue(key, new DriverDeleted(d.Id, d.UserId)); break;
        }
    }

    /// <summary>No driver exists for this user, so the key is the user id.</summary>
    public void DriverRejected(Guid userId, string licenseNumber, string reason) =>
        outbox.Enqueue(userId.ToString(), new DriverProfileRejected(userId, licenseNumber, reason));

    // ---- fleet.vehicle.v1 (key = vehicleId) ----------------------------------------------------
    public void VehicleCreated(Vehicle v) =>
        outbox.Enqueue(v.Id.ToString(), new VehicleCreated(v.Id, v.LicensePlate, v.Model, v.CapacityKg));

    public void VehicleUpdated(Vehicle v) =>
        outbox.Enqueue(v.Id.ToString(), new VehicleUpdated(v.Id, v.LicensePlate, v.Model, v.CapacityKg));

    public void VehicleAssigned(Vehicle v, Guid? previousDriverId) =>
        outbox.Enqueue(v.Id.ToString(), new VehicleAssigned(v.Id, v.AssignedDriverId, previousDriverId));

    public void VehicleStatusChanged(Vehicle v)
    {
        if (v.IsActive) outbox.Enqueue(v.Id.ToString(), new VehicleReactivated(v.Id));
        else outbox.Enqueue(v.Id.ToString(), new VehicleDeactivated(v.Id));
    }
}
