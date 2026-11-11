using System.Diagnostics;
using LastMile.Tracking.Data;
using LastMile.Tracking.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using StackExchange.Redis;

namespace LastMile.Tracking.Services.Background;

/// <summary>
/// Cold storage (T6): every 2 minutes, one replica copies the latest live position of every driver that pinged since
/// the previous run into <c>location_history</c>, in one bulk INSERT. Idempotent: the key is (driver_id, recorded_at).
/// </summary>
public sealed class PositionSampler(
    IServiceScopeFactory scopes,
    LivePositionStore live,
    RedisLock locks,
    IConnectionMultiplexer redis,
    IOptions<TrackingOptions> options,
    TimeProvider clock,
    ILogger<PositionSampler> logger) : PeriodicJob(logger)
{
    private const string CursorKey = "tracking:sampler:cursor";

    protected override TimeSpan Interval => options.Value.SampleInterval;

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        if (!await locks.TryAcquireAsync("sampler", Interval * 0.9)) return;

        var started = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var stopwatch = Stopwatch.StartNew();
        var db = redis.GetDatabase();
        var cursor = (long?)await db.StringGetAsync(CursorKey) ?? started - (long)Interval.TotalMilliseconds;

        var positions = await live.SinceAsync(cursor);
        var inserted = positions.Count == 0 ? 0 : await InsertAsync(positions, ct);

        // Advance only after a successful insert: a failed run is retried with the same window on the next tick.
        await db.StringSetAsync(CursorKey, started);
        logger.LogInformation("Sampled {Count} live positions into location_history ({Inserted} new) in {Elapsed} ms",
            positions.Count, inserted, stopwatch.ElapsedMilliseconds);
    }

    private async Task<int> InsertAsync(IReadOnlyList<LivePosition> p, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO location_history (driver_id, vehicle_id, shipment_id, latitude, longitude,
                                          speed_kmh, heading_deg, accuracy_m, recorded_at, received_at)
            SELECT * FROM unnest(@driver, @vehicle, @shipment, @lat, @lon, @speed, @heading, @accuracy, @recorded, @received)
            ON CONFLICT DO NOTHING
            """, conn);
        cmd.Parameters.Add(Array("driver", NpgsqlDbType.Uuid, p.Select(x => (object)x.DriverId)));
        cmd.Parameters.Add(Array("vehicle", NpgsqlDbType.Uuid, p.Select(x => (object?)x.VehicleId)));
        cmd.Parameters.Add(Array("shipment", NpgsqlDbType.Uuid, p.Select(x => (object?)x.ShipmentId)));
        cmd.Parameters.Add(Array("lat", NpgsqlDbType.Double, p.Select(x => (object)x.Latitude)));
        cmd.Parameters.Add(Array("lon", NpgsqlDbType.Double, p.Select(x => (object)x.Longitude)));
        cmd.Parameters.Add(Array("speed", NpgsqlDbType.Real, p.Select(x => (object?)(float?)x.SpeedKmh)));
        cmd.Parameters.Add(Array("heading", NpgsqlDbType.Real, p.Select(x => (object?)(float?)x.HeadingDeg)));
        cmd.Parameters.Add(Array("accuracy", NpgsqlDbType.Real, p.Select(x => (object?)(float?)x.AccuracyM)));
        cmd.Parameters.Add(Array("recorded", NpgsqlDbType.TimestampTz, p.Select(x => (object)x.RecordedAt.UtcDateTime)));
        cmd.Parameters.Add(Array("received", NpgsqlDbType.TimestampTz, p.Select(x => (object)x.ReceivedAt.UtcDateTime)));
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static NpgsqlParameter Array(string name, NpgsqlDbType type, IEnumerable<object?> values) =>
        new(name, NpgsqlDbType.Array | type) { Value = values.Select(v => v ?? DBNull.Value).ToArray() };
}
