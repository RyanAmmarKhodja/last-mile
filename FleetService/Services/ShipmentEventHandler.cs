using LastMile.Contracts.Shipment;
using LastMile.Fleet.Data;
using LastMile.Fleet.Enums;
using LastMile.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Fleet.Services;

/// <summary>
/// Applies <c>shipment.lifecycle.v1</c> to driver availability (decision D3, FleetService.md §8.3):
/// dispatched → unavailable; delivered / failed / cancelled-after-dispatch → available again when the driver is still
/// ACTIVE with an active vehicle. Idempotent: a redelivered event finds the flag already set and changes nothing.
/// </summary>
public sealed class ShipmentEventHandler(FleetDbContext db, FleetEventPublisher events, ILogger<ShipmentEventHandler> logger)
{
    public Task HandleAsync(ConsumedEvent evt, CancellationToken ct)
    {
        if (evt.Is<ShipmentDispatched>()) return SetAvailabilityAsync(evt.Data<ShipmentDispatched>().DriverId, false, evt.Type, ct);
        if (evt.Is<ShipmentDelivered>()) return SetAvailabilityAsync(evt.Data<ShipmentDelivered>().DriverId, true, evt.Type, ct);
        if (evt.Is<ShipmentFailed>()) return SetAvailabilityAsync(evt.Data<ShipmentFailed>().DriverId, true, evt.Type, ct);
        if (evt.Is<ShipmentCancelled>() && evt.Data<ShipmentCancelled>().DriverId is { } driverId)
            return SetAvailabilityAsync(driverId, true, evt.Type, ct);
        return Task.CompletedTask; // ShipmentCreated, ShipmentInTransit, cancellations before dispatch: nothing to do
    }

    private async Task SetAvailabilityAsync(Guid driverId, bool available, string cause, CancellationToken ct)
    {
        var entity = await db.Drivers.Include(d => d.Vehicle).FirstOrDefaultAsync(d => d.Id == driverId, ct);
        if (entity is null)
        {
            logger.LogWarning("{Event} references unknown driver {DriverId}", cause, driverId);
            return;
        }

        var driver = entity.ToDomain();
        var hasActiveVehicle = entity.Vehicle is { IsActive: true };
        // Back on duty only if still allowed (deactivated, deleted or vehicle-less drivers stay unavailable).
        if (available && (driver.Status != DriverStatus.ACTIVE || !hasActiveVehicle)) return;
        if (!driver.SetAvailable(available, hasActiveVehicle)) return;

        driver.ApplyTo(entity);
        events.AvailabilityChanged(driver, entity.Vehicle?.Id);
        logger.LogInformation("Driver {DriverId} availability = {Available} ({Event})", driver.Id, available, cause);
    }
}
