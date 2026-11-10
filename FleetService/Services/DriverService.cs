using System.Security.Claims;
using LastMile.Fleet.Core;
using LastMile.Fleet.Data;
using LastMile.Fleet.DTOs;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Fleet.Services;

/// <summary>Driver use cases: profile, availability (D3) and owner / admin / M2M access rules.</summary>
public sealed class DriverService(FleetDbContext db, FleetEventPublisher events, ILogger<DriverService> logger)
{
    private IQueryable<DriverEntity> Drivers => db.Drivers.Include(d => d.Vehicle);

    public async Task<DriverDto> GetMineAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId, ct);
        if (entity is null || entity.Status == nameof(DriverStatus.DELETED))
            throw new NotFoundException("No driver profile exists for this account yet. It is created shortly after " +
                                        "registration; you can also create it with PUT /api/v1/fleet/drivers/me.");
        return ToDto(entity);
    }

    /// <summary>Creates the caller's driver profile if missing (e.g. after DriverProfileRejected), otherwise replaces it.</summary>
    public async Task<(DriverDto Driver, bool Created)> UpsertMineAsync(ClaimsPrincipal actor, UpdateDriverDto dto,
        CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await Drivers.FirstOrDefaultAsync(d => d.UserId == userId, ct);
        if (entity is not null) return (await UpdateAsync(entity, dto, actor, ct), false);

        var driver = Driver.Create(userId, dto.ToDetails());
        await EnsureLicenseFreeAsync(driver.LicenseNumber, null, ct);
        db.Drivers.Add(driver.ToEntity());
        events.DriverCreated(driver); // same transaction as the row
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Driver {DriverId} created by owner {UserId}", driver.Id, userId);
        return (DriverDto.From(driver, null), true);
    }

    public async Task<DriverDto> SetMyAvailabilityAsync(ClaimsPrincipal actor, bool isAvailable, CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await Drivers.FirstOrDefaultAsync(d => d.UserId == userId, ct);
        if (entity is null || entity.Status == nameof(DriverStatus.DELETED))
            throw new NotFoundException("No driver profile exists for this account.");
        return await SetAvailabilityAsync(entity, isAvailable, actor, ct);
    }

    public async Task<DriverDto> SetAvailabilityAsync(Guid id, bool isAvailable, ClaimsPrincipal actor, CancellationToken ct) =>
        await SetAvailabilityAsync(await LoadAsync(id, ct), isAvailable, actor, ct);

    public async Task<PagedResultDto<DriverDto>> ListAsync(int page, int pageSize, string? search, DriverStatus? status,
        bool? available, CancellationToken ct)
    {
        (page, pageSize) = FleetQueries.Normalize(page, pageSize);
        var statusName = (status ?? DriverStatus.ACTIVE).ToString();
        var query = Drivers.AsNoTracking().Where(d => d.Status == statusName);
        if (available is not null) query = query.Where(d => d.IsAvailable == available);
        if (FleetQueries.SearchPattern(search) is { } pattern)
            query = query.Where(d => EF.Functions.ILike(d.FirstName, pattern) || EF.Functions.ILike(d.LastName, pattern) ||
                                     EF.Functions.ILike(d.LicenseNumber, pattern) || EF.Functions.ILike(d.PhoneNumber, pattern));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return FleetQueries.Page(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<DriverDto> GetAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct) =>
        Authorize(await Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct), actor);

    public async Task<DriverDto> GetByUserAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct) =>
        Authorize(await Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId, ct), actor);

    /// <summary>Administrator edit of any driver.</summary>
    public async Task<DriverDto> UpdateAsync(Guid id, UpdateDriverDto dto, ClaimsPrincipal actor, CancellationToken ct) =>
        await UpdateAsync(await LoadAsync(id, ct), dto, actor, ct);

    private async Task<DriverDto> UpdateAsync(DriverEntity entity, UpdateDriverDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var driver = entity.ToDomain();
        if (!driver.Update(dto.ToDetails())) return DriverDto.From(driver, entity.Vehicle);
        if (driver.LicenseNumber != entity.LicenseNumber)
            await EnsureLicenseFreeAsync(driver.LicenseNumber, driver.Id, ct);

        driver.ApplyTo(entity);
        events.DriverUpdated(driver);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Driver {DriverId} updated by {Actor}", driver.Id, actor.ActorId());
        return DriverDto.From(driver, entity.Vehicle);
    }

    private async Task<DriverDto> SetAvailabilityAsync(DriverEntity entity, bool isAvailable, ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var driver = entity.ToDomain();
        var vehicle = entity.Vehicle;
        if (driver.SetAvailable(isAvailable, hasActiveVehicle: vehicle is { IsActive: true }))
        {
            driver.ApplyTo(entity);
            events.AvailabilityChanged(driver, vehicle?.Id);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Driver {DriverId} availability = {Available} (by {Actor})", driver.Id, driver.IsAvailable,
                actor.ActorId());
        }
        return DriverDto.From(driver, vehicle);
    }

    private async Task<DriverEntity> LoadAsync(Guid id, CancellationToken ct) =>
        await Drivers.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Driver not found.");

    private async Task EnsureLicenseFreeAsync(string license, Guid? exceptDriverId, CancellationToken ct)
    {
        if (await db.Drivers.AnyAsync(d => d.LicenseNumber == license && d.Id != exceptDriverId, ct))
            throw new ConflictException("This license number is already registered to another driver.");
    }

    private static DriverDto ToDto(DriverEntity e) => DriverDto.From(e.ToDomain(), e.Vehicle);

    /// <summary>ADMIN and fleet:read services see every driver (including DELETED); a DRIVER sees only themselves.</summary>
    private static DriverDto Authorize(DriverEntity? entity, ClaimsPrincipal actor)
    {
        if (actor.CanReadFleet())
            return entity is null ? throw new NotFoundException("Driver not found.") : ToDto(entity);

        var userId = actor.GetUserId();
        if (entity is not null && entity.UserId != userId)
            throw new ForbiddenException("You can only access your own driver profile.");
        if (entity is null || entity.Status == nameof(DriverStatus.DELETED))
            throw new NotFoundException("Driver not found.");
        return ToDto(entity);
    }
}
