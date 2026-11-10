using System.Security.Claims;
using LastMile.Fleet.Core;
using LastMile.Fleet.Data;
using LastMile.Fleet.DTOs;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Fleet.Services;

/// <summary>
/// Vehicle use cases, including the driver ↔ vehicle assignment and its effect on availability:
/// a driver who loses their vehicle (unassigned, reassigned, retired) becomes unavailable.
/// </summary>
public sealed class VehicleService(FleetDbContext db, FleetEventPublisher events, ILogger<VehicleService> logger)
{
    public async Task<PagedResultDto<VehicleDto>> ListAsync(int page, int pageSize, string? search, bool? active,
        bool? assigned, CancellationToken ct)
    {
        (page, pageSize) = FleetQueries.Normalize(page, pageSize);
        var isActive = active ?? true;
        var query = db.Vehicles.AsNoTracking().Where(v => v.IsActive == isActive);
        if (assigned is not null) query = query.Where(v => (v.AssignedDriverId != null) == assigned);
        if (FleetQueries.SearchPattern(search) is { } pattern)
            query = query.Where(v => EF.Functions.ILike(v.LicensePlate, pattern) || EF.Functions.ILike(v.Model, pattern));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(v => v.LicensePlate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return FleetQueries.Page(items.Select(e => VehicleDto.From(e.ToDomain())).ToList(), page, pageSize, total);
    }

    /// <summary>ADMIN / fleet:read services see any vehicle; a DRIVER only the vehicle assigned to them.</summary>
    public async Task<VehicleDto> GetAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (!actor.CanReadFleet())
        {
            var userId = actor.GetUserId();
            var isMine = entity?.AssignedDriverId is { } driverId &&
                         await db.Drivers.AnyAsync(d => d.Id == driverId && d.UserId == userId, ct);
            if (entity is not null && !isMine) throw new ForbiddenException("You can only access the vehicle assigned to you.");
        }
        return entity is null ? throw new NotFoundException("Vehicle not found.") : VehicleDto.From(entity.ToDomain());
    }

    public async Task<VehicleDto> CreateAsync(VehicleDetailsDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var vehicle = Vehicle.Create(dto.ToDetails());
        await EnsurePlateFreeAsync(vehicle.LicensePlate, null, ct);
        db.Vehicles.Add(vehicle.ToEntity());
        events.VehicleCreated(vehicle);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Vehicle {VehicleId} ({Plate}) created by {Actor}", vehicle.Id, vehicle.LicensePlate, actor.ActorId());
        return VehicleDto.From(vehicle);
    }

    public async Task<VehicleDto> UpdateAsync(Guid id, VehicleDetailsDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await LoadAsync(id, ct);
        var vehicle = entity.ToDomain();
        if (!vehicle.Update(dto.ToDetails())) return VehicleDto.From(vehicle);
        if (vehicle.LicensePlate != entity.LicensePlate) await EnsurePlateFreeAsync(vehicle.LicensePlate, vehicle.Id, ct);

        vehicle.ApplyTo(entity);
        events.VehicleUpdated(vehicle);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Vehicle {VehicleId} updated by {Actor}", vehicle.Id, actor.ActorId());
        return VehicleDto.From(vehicle);
    }

    /// <summary>Assigns the vehicle to <paramref name="driverId"/>, or unassigns it when null.</summary>
    public async Task<VehicleDto> AssignAsync(Guid id, Guid? driverId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await LoadAsync(id, ct);
        var vehicle = entity.ToDomain();
        if (driverId == vehicle.AssignedDriverId) return VehicleDto.From(vehicle);

        if (driverId is not null)
        {
            var driver = await db.Drivers.Include(d => d.Vehicle).FirstOrDefaultAsync(d => d.Id == driverId, ct)
                         ?? throw new NotFoundException("Driver not found.");
            if (driver.Status != nameof(DriverStatus.ACTIVE))
                throw new ConflictException($"A driver with status {driver.Status} cannot be assigned a vehicle.");
            if (driver.Vehicle is not null && driver.Vehicle.Id != vehicle.Id)
                throw new ConflictException(
                    $"This driver already operates vehicle {driver.Vehicle.LicensePlate}. Unassign it first.");
        }

        var previousDriverId = vehicle.AssignedDriverId;
        vehicle.AssignTo(driverId);
        vehicle.ApplyTo(entity);
        if (previousDriverId is not null) await EndShiftAsync(previousDriverId.Value, ct);
        events.VehicleAssigned(vehicle, previousDriverId);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Vehicle {VehicleId} assigned to {DriverId} (was {PreviousDriverId}) by {Actor}",
            vehicle.Id, driverId, previousDriverId, actor.ActorId());
        return VehicleDto.From(vehicle);
    }

    /// <summary>Retires (unassigning first) or reactivates a vehicle.</summary>
    public async Task<VehicleDto> SetActiveAsync(Guid id, bool isActive, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await LoadAsync(id, ct);
        var vehicle = entity.ToDomain();

        if (!isActive && vehicle.AssignedDriverId is { } previousDriverId)
        {
            vehicle.AssignTo(null);
            await EndShiftAsync(previousDriverId, ct);
            events.VehicleAssigned(vehicle, previousDriverId);
        }

        if (isActive ? vehicle.Reactivate() : vehicle.Retire())
        {
            vehicle.ApplyTo(entity);
            events.VehicleStatusChanged(vehicle);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Vehicle {VehicleId} active = {Active} (by {Actor})", vehicle.Id, vehicle.IsActive, actor.ActorId());
        }
        return VehicleDto.From(vehicle);
    }

    /// <summary>
    /// Releases a vehicle from a driver who is being deleted (UserDeleted). Availability is handled by the caller.
    /// </summary>
    public void Release(VehicleEntity entity)
    {
        var vehicle = entity.ToDomain();
        var previousDriverId = vehicle.AssignedDriverId;
        if (!vehicle.AssignTo(null)) return;
        vehicle.ApplyTo(entity);
        events.VehicleAssigned(vehicle, previousDriverId);
    }

    /// <summary>The driver no longer has a vehicle: they cannot stay available.</summary>
    private async Task EndShiftAsync(Guid driverId, CancellationToken ct)
    {
        var entity = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, ct);
        if (entity is null) return;
        var driver = entity.ToDomain();
        if (!driver.SetAvailable(false, hasActiveVehicle: false)) return;
        driver.ApplyTo(entity);
        events.AvailabilityChanged(driver, null);
        logger.LogInformation("Driver {DriverId} is no longer available (vehicle removed)", driver.Id);
    }

    private async Task<VehicleEntity> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new NotFoundException("Vehicle not found.");

    private async Task EnsurePlateFreeAsync(string plate, Guid? exceptVehicleId, CancellationToken ct)
    {
        if (await db.Vehicles.AnyAsync(v => v.LicensePlate == plate && v.Id != exceptVehicleId, ct))
            throw new ConflictException("A vehicle with this license plate already exists.");
    }
}
