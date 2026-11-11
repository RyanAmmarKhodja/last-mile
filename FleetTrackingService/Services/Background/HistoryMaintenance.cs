using System.Globalization;
using LastMile.Tracking.Data;
using LastMile.Tracking.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Services.Background;

/// <summary>
/// Retention (FR-7, T5): creates the daily partitions of <c>location_history</c> ahead of time and drops the ones whose
/// day ended more than <see cref="TrackingOptions.HistoryRetentionDays"/> ago (a DROP, no DELETE, no bloat). Runs at
/// startup and every hour; one replica at a time thanks to a transaction-scoped advisory lock.
/// </summary>
public sealed class HistoryMaintenance(
    IServiceScopeFactory scopes,
    IOptions<TrackingOptions> options,
    TimeProvider clock,
    ILogger<HistoryMaintenance> logger) : PeriodicJob(logger)
{
    private const long LockKey = 0x7472_6163_6b00; // "track"
    private const string Prefix = "location_history_p";

    protected override TimeSpan Interval => options.Value.MaintenanceInterval;
    protected override bool RunAtStartup => true;

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var result = await RunAsync(db, options.Value, clock.GetUtcNow(), ct);
        if (result.Created.Count + result.Dropped.Count + result.DefaultRowsDeleted > 0)
            logger.LogInformation("History partitions: created [{Created}], dropped [{Dropped}], {Rows} expired default rows deleted",
                string.Join(", ", result.Created), string.Join(", ", result.Dropped), result.DefaultRowsDeleted);
    }

    public sealed record Result(IReadOnlyList<string> Created, IReadOnlyList<string> Dropped, int DefaultRowsDeleted);

    /// <summary>Also called by <see cref="DatabaseInitializer"/> so partitions exist before the first sample.</summary>
    public static async Task<Result> RunAsync(TrackingDbContext db, TrackingOptions o, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({LockKey}) AS \"Value\"").SingleAsync(ct);
        if (!locked) return new([], [], 0);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var existing = (await db.Database.SqlQuery<string>($"""
            SELECT c.relname AS "Value" FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid JOIN pg_class p ON p.oid = i.inhparent
            WHERE p.relname = 'location_history'
            """).ToListAsync(ct)).ToHashSet();

        var created = new List<string>();
        for (var day = today.AddDays(-1); day <= today.AddDays(o.PartitionsAhead); day = day.AddDays(1))
        {
            var name = Prefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (existing.Contains(name)) continue;
            // Identifiers and bounds come from dates formatted here, never from input.
#pragma warning disable EF1002, EF1003
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE TABLE IF NOT EXISTS {name} PARTITION OF location_history " +
                $"FOR VALUES FROM ('{day:yyyy-MM-dd} 00:00:00+00') TO ('{day.AddDays(1):yyyy-MM-dd} 00:00:00+00')", ct);
#pragma warning restore EF1002, EF1003
            created.Add(name);
        }

        // A day's partition is dropped once its whole day is older than the retention window.
        var cutoff = now.UtcDateTime.AddDays(-o.HistoryRetentionDays);
        var dropped = new List<string>();
        foreach (var name in existing.Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            if (!DateOnly.TryParseExact(name[Prefix.Length..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                continue;
            if (day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) > cutoff) continue;
#pragma warning disable EF1002, EF1003
            await db.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {name}", ct);
#pragma warning restore EF1002, EF1003
            dropped.Add(name);
        }

        var cutoffOffset = new DateTimeOffset(cutoff, TimeSpan.Zero);
        var defaultRows = await db.Database.ExecuteSqlAsync(
            $"DELETE FROM location_history_default WHERE recorded_at < {cutoffOffset}", ct);

        await tx.CommitAsync(ct);
        return new(created, dropped, defaultRows);
    }
}
