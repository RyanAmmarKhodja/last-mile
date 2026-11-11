using LastMile.Tracking.Options;
using LastMile.Tracking.Services.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies EF Core migrations (retrying while PostgreSQL starts), then makes sure today's and the next days'
    /// <c>location_history</c> partitions exist before the first sample.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

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

        var options = scope.ServiceProvider.GetRequiredService<IOptions<TrackingOptions>>().Value;
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var result = await HistoryMaintenance.RunAsync(db, options, clock.GetUtcNow(), ct);
        logger.LogInformation("History partitions ready (created {Created})", result.Created.Count);
    }
}
