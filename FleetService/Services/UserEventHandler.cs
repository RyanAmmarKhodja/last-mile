using LastMile.Contracts.Auth;
using LastMile.Fleet.Core;
using LastMile.Fleet.Data;
using LastMile.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Fleet.Services;

/// <summary>
/// Applies <c>auth.user.v1</c> events to drivers (documents/FleetService/FleetService.md §8.1). Runs inside the
/// consumer's transaction: changes, outgoing events and the inbox row commit together. Every branch is idempotent.
/// </summary>
public sealed class UserEventHandler(
    FleetDbContext db,
    FleetEventPublisher events,
    VehicleService vehicles,
    ILogger<UserEventHandler> logger)
{
    public Task HandleAsync(ConsumedEvent evt, CancellationToken ct)
    {
        if (evt.Is<UserRegistered>()) return OnRegisteredAsync(evt.Data<UserRegistered>(), ct);
        if (evt.Is<UserDeactivated>()) return DeactivateAsync(evt.Data<UserDeactivated>().UserId, ct);
        if (evt.Is<UserReactivated>())
        {
            var e = evt.Data<UserReactivated>();
            return e.Role == UserRole.DRIVER ? ReactivateAsync(e.UserId, ct) : Task.CompletedTask;
        }
        if (evt.Is<UserRoleChanged>()) return OnRoleChangedAsync(evt.Data<UserRoleChanged>(), ct);
        if (evt.Is<UserDeleted>()) return DeleteAsync(evt.Data<UserDeleted>().UserId, ct);
        return Task.CompletedTask; // UserEmailChanged and future types: not relevant here
    }

    private async Task OnRegisteredAsync(UserRegistered e, CancellationToken ct)
    {
        if (e.Role != UserRole.DRIVER || await db.Drivers.AnyAsync(d => d.UserId == e.UserId, ct)) return;

        Driver driver;
        try
        {
            driver = Driver.Create(e.UserId,
                new DriverDetails(e.Profile.FirstName, e.Profile.LastName, e.Profile.PhoneNumber, e.Profile.LicenseNumber));
        }
        catch (DomainValidationException ex)
        {
            // AuthService validates the same rules, so this means a broken producer: retrying cannot help.
            throw new PermanentEventException($"UserRegistered for {e.UserId} has an invalid driver profile: {ex.Message}", ex);
        }

        if (await db.Drivers.AnyAsync(d => d.LicenseNumber == driver.LicenseNumber, ct))
        {
            // Business rejection: tell the world (matrix §8.1), keep consuming.
            events.DriverRejected(e.UserId, driver.LicenseNumber, "License number is already registered to another driver.");
            logger.LogWarning("Driver profile for user {UserId} rejected: license {License} already registered",
                e.UserId, driver.LicenseNumber);
            return;
        }

        db.Drivers.Add(driver.ToEntity());
        events.DriverCreated(driver);
        logger.LogInformation("Driver {DriverId} created for user {UserId}", driver.Id, e.UserId);
    }

    private async Task OnRoleChangedAsync(UserRoleChanged e, CancellationToken ct)
    {
        if (e.PreviousRole == UserRole.DRIVER) await DeactivateAsync(e.UserId, ct);
        if (e.Role == UserRole.DRIVER) await ReactivateAsync(e.UserId, ct);
    }

    private async Task DeactivateAsync(Guid userId, CancellationToken ct)
    {
        if (await LoadAsync(userId, ct) is not { } entity) return;
        var driver = entity.ToDomain();
        var wasAvailable = driver.IsAvailable;
        if (!driver.Deactivate()) return;

        driver.ApplyTo(entity);
        if (wasAvailable) events.AvailabilityChanged(driver, entity.Vehicle?.Id);
        events.DriverStatusChanged(driver);
        logger.LogInformation("Driver {DriverId} is now {Status}", driver.Id, driver.Status);
    }

    private async Task ReactivateAsync(Guid userId, CancellationToken ct)
    {
        if (await LoadAsync(userId, ct) is not { } entity) return;
        var driver = entity.ToDomain();
        if (!driver.Reactivate()) return;

        driver.ApplyTo(entity);
        events.DriverStatusChanged(driver);
        logger.LogInformation("Driver {DriverId} is now {Status}", driver.Id, driver.Status);
    }

    private async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        if (await LoadAsync(userId, ct) is not { } entity) return;
        var driver = entity.ToDomain();
        var wasAvailable = driver.IsAvailable;
        if (!driver.MarkDeleted()) return;

        driver.ApplyTo(entity);
        if (wasAvailable) events.AvailabilityChanged(driver, null);
        if (entity.Vehicle is { } vehicle) vehicles.Release(vehicle);
        events.DriverStatusChanged(driver);
        logger.LogInformation("Driver {DriverId} is now {Status}", driver.Id, driver.Status);
    }

    private Task<Models.DriverEntity?> LoadAsync(Guid userId, CancellationToken ct) =>
        db.Drivers.Include(d => d.Vehicle).FirstOrDefaultAsync(d => d.UserId == userId, ct);
}
