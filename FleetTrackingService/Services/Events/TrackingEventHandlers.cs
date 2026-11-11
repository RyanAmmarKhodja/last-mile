using LastMile.Contracts.Fleet;
using LastMile.Contracts.Shipment;
using LastMile.Messaging;
using LastMile.Messaging.Inbox;
using LastMile.Tracking.Data;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Services.Events;

/// <summary>
/// Kafka consumer (group <c>tracking-service</c>) of <c>fleet.driver.v1</c> and <c>shipment.lifecycle.v1</c>
/// (FleetTrackingService.md §8). The base class provides inbox de-duplication, retries and the DLQ.
/// </summary>
public sealed class TrackingEventsConsumer(
    IServiceScopeFactory scopes,
    KafkaProducer kafka,
    MessagingService service,
    IOptions<MessagingOptions> options,
    ILogger<TrackingEventsConsumer> logger) : EventConsumer<TrackingDbContext>(scopes, kafka, service, options, logger)
{
    protected override IReadOnlyCollection<string> Topics =>
        [LastMile.Contracts.Topics.FleetDriverV1, LastMile.Contracts.Topics.ShipmentLifecycleV1];

    protected override Task HandleAsync(ConsumedEvent evt, TrackingDbContext db, IServiceProvider services, CancellationToken ct) =>
        evt.Topic switch
        {
            LastMile.Contracts.Topics.FleetDriverV1 => services.GetRequiredService<DriverEventHandler>().HandleAsync(evt, ct),
            LastMile.Contracts.Topics.ShipmentLifecycleV1 => services.GetRequiredService<ShipmentEventHandler>().HandleAsync(evt, ct),
            _ => Task.CompletedTask
        };
}

/// <summary>
/// <c>fleet.driver.v1</c> → <c>tracked_drivers</c>. Every event carries the user id, so each one upserts (a driver created
/// before the topic's 30-day window still appears on their next event). Going off shift or being deactivated removes
/// the driver from the live map at once (FR-3); that Redis side effect is best-effort.
/// </summary>
public sealed class DriverEventHandler(
    TrackingDbContext db,
    LivePositionStore live,
    DriverContextCache contexts,
    TimeProvider clock,
    ILogger<DriverEventHandler> logger)
{
    public async Task HandleAsync(ConsumedEvent evt, CancellationToken ct)
    {
        if (evt.Is<DriverCreated>())
        {
            var e = evt.Data<DriverCreated>();
            await UpsertAsync(e.DriverId, e.UserId, ct);
        }
        else if (evt.Is<DriverUpdated>())
        {
            var e = evt.Data<DriverUpdated>();
            await UpsertAsync(e.DriverId, e.UserId, ct);
        }
        else if (evt.Is<DriverAvailabilityChanged>())
        {
            var e = evt.Data<DriverAvailabilityChanged>();
            var driver = await UpsertAsync(e.DriverId, e.UserId, ct);
            driver.IsAvailable = e.IsAvailable;
            driver.VehicleId = e.VehicleId;
            if (!e.IsAvailable && !await HasActiveShipmentAsync(e.DriverId, ct))
                await TakeOffMapAsync(e.DriverId, "off shift");
        }
        else if (evt.Is<DriverDeactivated>())
        {
            var e = evt.Data<DriverDeactivated>();
            await SetStatusAsync(e.DriverId, e.UserId, DriverStatuses.Inactive, ct);
        }
        else if (evt.Is<DriverDeleted>())
        {
            var e = evt.Data<DriverDeleted>();
            await SetStatusAsync(e.DriverId, e.UserId, DriverStatuses.Deleted, ct);
        }
        else if (evt.Is<DriverReactivated>())
        {
            var e = evt.Data<DriverReactivated>();
            (await UpsertAsync(e.DriverId, e.UserId, ct)).Status = DriverStatuses.Active;
        }
        // DriverProfileRejected: no driver exists, nothing to track.
    }

    private async Task SetStatusAsync(Guid driverId, Guid userId, string status, CancellationToken ct)
    {
        var driver = await UpsertAsync(driverId, userId, ct);
        driver.Status = status;
        driver.IsAvailable = false;
        await TakeOffMapAsync(driverId, status);
    }

    private async Task<TrackedDriverEntity> UpsertAsync(Guid driverId, Guid userId, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId, ct);
        if (driver is null)
        {
            driver = new TrackedDriverEntity { DriverId = driverId, UserId = userId, Status = DriverStatuses.Active };
            db.Drivers.Add(driver);
        }
        else if (driver.UserId != userId)
        {
            contexts.Invalidate(driverId, driver.UserId);
            driver.UserId = userId;
        }
        driver.UpdatedAt = clock.GetUtcNow();
        contexts.Invalidate(driverId, userId);
        return driver;
    }

    private Task<bool> HasActiveShipmentAsync(Guid driverId, CancellationToken ct) =>
        db.Shipments.AnyAsync(s => s.DriverId == driverId &&
                                   (s.Status == ShipmentStatuses.Dispatched || s.Status == ShipmentStatuses.InTransit), ct);

    private async Task TakeOffMapAsync(Guid driverId, string reason)
    {
        try
        {
            await live.RemoveAsync(driverId);
            logger.LogInformation("Driver {DriverId} removed from the live map ({Reason})", driverId, reason);
        }
        catch (Exception ex)
        {
            // Best-effort: the 10-minute TTL removes the driver anyway. Never fail the event for it.
            logger.LogWarning("Could not remove driver {DriverId} from the live map: {Message}", driverId, ex.Message);
        }
    }
}

/// <summary>
/// <c>shipment.lifecycle.v1</c> → <c>tracked_shipments</c> (tracking number, status, driver, vehicle). Events of one
/// shipment arrive in order (key = shipmentId); a final status is never overwritten by a non-final one (replay guard).
/// </summary>
public sealed class ShipmentEventHandler(TrackingDbContext db, DriverContextCache contexts, TimeProvider clock)
{
    public Task HandleAsync(ConsumedEvent evt, CancellationToken ct)
    {
        if (evt.Is<ShipmentCreated>())
        {
            var e = evt.Data<ShipmentCreated>();
            return ApplyAsync(e.ShipmentId, e.TrackingNumber, ShipmentStatuses.Created, null, null, ct);
        }
        if (evt.Is<ShipmentDispatched>())
        {
            var e = evt.Data<ShipmentDispatched>();
            return ApplyAsync(e.ShipmentId, e.TrackingNumber, ShipmentStatuses.Dispatched, e.DriverId, e.VehicleId, ct);
        }
        if (evt.Is<ShipmentInTransit>())
        {
            var e = evt.Data<ShipmentInTransit>();
            return ApplyAsync(e.ShipmentId, e.TrackingNumber, ShipmentStatuses.InTransit, e.DriverId, null, ct);
        }
        if (evt.Is<ShipmentDelivered>())
        {
            var e = evt.Data<ShipmentDelivered>();
            return ApplyAsync(e.ShipmentId, e.TrackingNumber, ShipmentStatuses.Delivered, e.DriverId, null, ct);
        }
        if (evt.Is<ShipmentFailed>())
        {
            var e = evt.Data<ShipmentFailed>();
            return ApplyAsync(e.ShipmentId, e.TrackingNumber, ShipmentStatuses.Failed, e.DriverId, null, ct);
        }
        if (evt.Is<ShipmentCancelled>())
        {
            var e = evt.Data<ShipmentCancelled>();
            return ApplyAsync(e.ShipmentId, e.TrackingNumber, ShipmentStatuses.Cancelled, e.DriverId, null, ct);
        }
        return Task.CompletedTask;
    }

    private async Task ApplyAsync(Guid shipmentId, string trackingNumber, string status, Guid? driverId, Guid? vehicleId,
        CancellationToken ct)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct);
        if (shipment is null)
        {
            shipment = new TrackedShipmentEntity
            {
                ShipmentId = shipmentId,
                TrackingNumber = Core.TrackingRules.NormalizeTrackingNumber(trackingNumber),
                Status = status
            };
            db.Shipments.Add(shipment);
        }
        else if (ShipmentStatuses.IsFinal(shipment.Status) && !ShipmentStatuses.IsFinal(status))
        {
            return; // replayed older event after the outcome: keep the outcome
        }

        shipment.Status = status;
        if (driverId is not null) shipment.DriverId = driverId;
        if (vehicleId is not null) shipment.VehicleId = vehicleId;
        shipment.UpdatedAt = clock.GetUtcNow();

        if (shipment.DriverId is { } d) contexts.Invalidate(d);
    }
}
