using Microsoft.EntityFrameworkCore;

namespace LastMile.Shipments.Data;

public static class DatabaseInitializer
{
    /// <summary>Applies EF Core migrations, retrying while PostgreSQL starts. No seed: shipments are created through the API.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();

        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database schema is up to date");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is Npgsql.NpgsqlException or TimeoutException)
            {
                logger.LogWarning("Database not reachable (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
