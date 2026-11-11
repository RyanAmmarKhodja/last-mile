using System.Security.Claims;
using LastMile.Shipments.Core;
using LastMile.Shipments.Data;
using LastMile.Shipments.DTOs;
using LastMile.Shipments.Enums;
using LastMile.Shipments.Services.Clients;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Shipments.Services;

/// <summary>
/// Dispatch (decision S2): to a chosen driver, or auto-pick the available driver whose vehicle is the smallest that fits
/// the weight. Availability is read fresh from FleetService; "one active shipment per driver" is checked locally and
/// enforced by the ux_shipments_driver_active index (D3, closes the race while FleetService catches up).
/// </summary>
public sealed class DispatchService(
    ShipmentDbContext db,
    ShipmentService shipments,
    FleetClient fleet,
    ILogger<DispatchService> logger)
{
    private static readonly string[] ActiveStatuses = [nameof(ShipmentStatus.DISPATCHED), nameof(ShipmentStatus.IN_TRANSIT)];

    public async Task<ShipmentDto> DispatchAsync(Guid id, Guid? driverId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await shipments.LoadAsync(id, tracking: true, ct);
        var shipment = entity.ToDomain();
        if (shipment.Status != ShipmentStatus.CREATED) // fail before any remote call
            throw new ConflictException($"A {shipment.Status} shipment cannot be dispatched (expected CREATED).");

        var driver = driverId is { } chosen
            ? await ValidateChosenAsync(chosen, shipment.WeightKg, ct)
            : await PickDriverAsync(shipment.WeightKg, ct);

        var entry = shipment.Dispatch(driver.Id, driver.UserId, driver.Vehicle!.Id, actor.ActorId());
        logger.LogInformation("Dispatching {TrackingNumber} to driver {DriverId} with vehicle {Plate} ({Mode})",
            shipment.TrackingNumber, driver.Id, driver.Vehicle.LicensePlate, driverId is null ? "auto" : "chosen");
        return await shipments.ApplyAsync(entity, shipment, entry, actor, ct);
    }

    private async Task<FleetDriver> ValidateChosenAsync(Guid driverId, decimal weightKg, CancellationToken ct)
    {
        var driver = await fleet.GetDriverAsync(driverId, ct) ?? throw new NotFoundException("Driver not found.");
        if (!driver.IsActive) throw new ConflictException($"The driver is {driver.Status}.");
        if (!driver.IsAvailable) throw new ConflictException("The driver is not available (not on shift, or already busy).");
        if (driver.Vehicle is null) throw new ConflictException("The driver has no vehicle.");
        if (driver.Vehicle.CapacityKg < weightKg)
            throw new ConflictException($"The driver's vehicle ({driver.Vehicle.CapacityKg} kg) cannot carry {weightKg} kg.");
        if (await IsBusyAsync(driverId, ct)) throw new ConflictException("This driver already has an active shipment.");
        return driver;
    }

    private async Task<FleetDriver> PickDriverAsync(decimal weightKg, CancellationToken ct)
    {
        var busy = await db.Shipments.AsNoTracking()
            .Where(s => s.AssignedDriverId != null && ActiveStatuses.Contains(s.CurrentStatus))
            .Select(s => s.AssignedDriverId!.Value).ToListAsync(ct);

        var candidates = (await fleet.ListAvailableDriversAsync(ct))
            .Where(d => d.IsActive && d.IsAvailable && d.Vehicle is not null && d.Vehicle.CapacityKg >= weightKg && !busy.Contains(d.Id))
            .OrderBy(d => d.Vehicle!.CapacityKg).ThenBy(d => d.Id)
            .ToList();
        return candidates.FirstOrDefault()
               ?? throw new ConflictException($"No available driver has a vehicle that can carry {weightKg} kg.");
    }

    private Task<bool> IsBusyAsync(Guid driverId, CancellationToken ct) =>
        db.Shipments.AnyAsync(s => s.AssignedDriverId == driverId && ActiveStatuses.Contains(s.CurrentStatus), ct);
}
