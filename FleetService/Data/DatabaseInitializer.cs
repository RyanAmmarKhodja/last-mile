using LastMile.Fleet.Core;
using LastMile.Fleet.Options;
using LastMile.Fleet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Fleet.Data;

public static class DatabaseInitializer
{
    /// <summary>Applies EF Core migrations (retrying while PostgreSQL starts) and seeds demo vehicles (Development).</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database schema is up to date");
                break;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is Npgsql.NpgsqlException or TimeoutException)
            {
                logger.LogWarning("Database not reachable (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var events = scope.ServiceProvider.GetRequiredService<FleetEventPublisher>();
        foreach (var v in seed.Vehicles.Where(v => !string.IsNullOrWhiteSpace(v.LicensePlate)))
        {
            var plate = FleetRules.NormalizePlate(v.LicensePlate);
            if (await db.Vehicles.AnyAsync(x => x.LicensePlate == plate, ct)) continue;

            var vehicle = Vehicle.Create(new VehicleDetails(v.LicensePlate, v.Model, v.CapacityKg));
            db.Vehicles.Add(vehicle.ToEntity());
            events.VehicleCreated(vehicle); // same transaction as the row
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded demo vehicle {Plate}", vehicle.LicensePlate);
        }
    }
}
