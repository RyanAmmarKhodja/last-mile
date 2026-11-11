using System.Globalization;
using System.Text.Json;
using LastMile.Tracking.Core;
using LastMile.Tracking.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LastMile.Tracking.Services;

/// <summary>Latest known position of a driver (Redis hash <c>tracking:driver:{id}</c>).</summary>
public sealed record LivePosition(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    DateTimeOffset ReceivedAt,
    double? SpeedKmh,
    double? HeadingDeg,
    double? AccuracyM,
    Guid? VehicleId,
    Guid? ShipmentId,
    string? TrackingNumber);

/// <summary>Message on the <c>tracking:positions</c> channel: <c>position</c> (with fields) or <c>offline</c>.</summary>
public sealed record LiveMessage(string Type, Guid DriverId, LivePosition? Position = null);

/// <summary>
/// Hot storage in Redis (FleetTrackingService.md §4.1). Every write is one Lua script, so the
/// "newer than the stored ping" check, the hash, the GEO set, the last-seen ZSET and the pub/sub message are atomic
/// and cost one round trip (NFR-1).
/// </summary>
public sealed class LivePositionStore(IConnectionMultiplexer redis, IOptions<TrackingOptions> options)
{
    public const string GeoKey = "tracking:geo";
    public const string LastSeenKey = "tracking:lastseen";
    public const string Channel = "tracking:positions";
    public static string DriverKey(Guid driverId) => $"tracking:driver:{driverId:D}";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // KEYS: hash, geo, lastseen · ARGV: driverId, lat, lon, ts, rcv, ttlSeconds, channel, payload, [field, value]...
    private const string WriteScript = """
        local cur = redis.call('HGET', KEYS[1], 'ts')
        if cur and tonumber(cur) >= tonumber(ARGV[4]) then return 0 end
        redis.call('DEL', KEYS[1])
        redis.call('HSET', KEYS[1], 'lat', ARGV[2], 'lon', ARGV[3], 'ts', ARGV[4], 'rcv', ARGV[5], unpack(ARGV, 9))
        redis.call('EXPIRE', KEYS[1], ARGV[6])
        redis.call('GEOADD', KEYS[2], ARGV[3], ARGV[2], ARGV[1])
        redis.call('ZADD', KEYS[3], ARGV[5], ARGV[1])
        redis.call('PUBLISH', ARGV[7], ARGV[8])
        return 1
        """;

    // KEYS: geo, lastseen · ARGV: cutoff (server ms), channel. Only members still older than the cutoff are removed.
    private const string SweepScript = """
        local stale = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', ARGV[1], 'LIMIT', 0, 1000)
        for _, id in ipairs(stale) do
          redis.call('ZREM', KEYS[1], id)
          redis.call('ZREM', KEYS[2], id)
          redis.call('PUBLISH', ARGV[2], '{"type":"offline","driverId":"' .. id .. '"}')
        end
        return #stale
        """;

    private IDatabase Db => redis.GetDatabase();

    /// <summary>Stores the ping if it is newer than the live one. Returns false when it was ignored (older or equal).</summary>
    public async Task<bool> WriteAsync(DriverContext ctx, Ping ping, DateTimeOffset receivedAt)
    {
        var position = new LivePosition(ctx.DriverId, ping.Latitude, ping.Longitude, ping.RecordedAt, receivedAt,
            ping.SpeedKmh, ping.HeadingDeg, ping.AccuracyM, ctx.EffectiveVehicleId, ctx.ShipmentId, ctx.TrackingNumber);
        var payload = JsonSerializer.Serialize(new LiveMessage("position", ctx.DriverId, position), Json);

        var args = new List<RedisValue>(20)
        {
            ctx.DriverId.ToString("D"), F(ping.Latitude), F(ping.Longitude),
            ping.RecordedAt.ToUnixTimeMilliseconds(), receivedAt.ToUnixTimeMilliseconds(),
            (long)options.Value.LiveTtl.TotalSeconds, Channel, payload
        };
        void Add(string field, string? value)
        {
            if (value is null) return;
            args.Add(field);
            args.Add(value);
        }
        Add("spd", ping.SpeedKmh is { } s ? F(s) : null);
        Add("hdg", ping.HeadingDeg is { } h ? F(h) : null);
        Add("acc", ping.AccuracyM is { } a ? F(a) : null);
        Add("veh", ctx.EffectiveVehicleId?.ToString("D"));
        Add("shp", ctx.ShipmentId?.ToString("D"));
        Add("trk", ctx.TrackingNumber);

        var result = await Db.ScriptEvaluateAsync(WriteScript,
            [DriverKey(ctx.DriverId), GeoKey, LastSeenKey], args.ToArray());
        return (int)result == 1;
    }

    public async Task<LivePosition?> GetAsync(Guid driverId) =>
        Parse(driverId, await Db.HashGetAllAsync(DriverKey(driverId)));

    /// <summary>Every live driver (members of the last-seen set whose hash has not expired).</summary>
    public async Task<IReadOnlyList<LivePosition>> GetAllAsync()
    {
        var members = await Db.SortedSetRangeByRankAsync(LastSeenKey);
        return await LoadAsync(members);
    }

    /// <summary>Drivers that pinged after <paramref name="cursorMs"/> (server time), for the sampler.</summary>
    public async Task<IReadOnlyList<LivePosition>> SinceAsync(long cursorMs)
    {
        var members = await Db.SortedSetRangeByScoreAsync(LastSeenKey, cursorMs, double.PositiveInfinity, Exclude.Start);
        return await LoadAsync(members);
    }

    /// <summary>Live drivers within <paramref name="radiusKm"/>, closest first.</summary>
    public async Task<IReadOnlyList<(LivePosition Position, double DistanceKm)>> NearbyAsync(double latitude,
        double longitude, double radiusKm, int limit)
    {
        var hits = await Db.GeoSearchAsync(GeoKey, longitude, latitude, new GeoSearchCircle(radiusKm, GeoUnit.Kilometers),
            limit, demandClosest: true, Order.Ascending, GeoRadiusOptions.WithDistance);
        var batch = Db.CreateBatch();
        var loads = hits.Select(h => (Hit: h, Task: batch.HashGetAllAsync(DriverKey(Guid.Parse(h.Member.ToString()))))).ToList();
        batch.Execute();
        var result = new List<(LivePosition, double)>(loads.Count);
        foreach (var (hit, task) in loads)
            if (Parse(Guid.Parse(hit.Member.ToString()), await task) is { } p)
                result.Add((p, Math.Round(hit.Distance ?? 0, 3)));
        return result;
    }

    /// <summary>Removes a driver from the live map now (off shift, deactivated) and tells the streams.</summary>
    public async Task RemoveAsync(Guid driverId)
    {
        var id = driverId.ToString("D");
        var tx = Db.CreateTransaction();
        _ = tx.KeyDeleteAsync(DriverKey(driverId));
        _ = tx.SortedSetRemoveAsync(GeoKey, id);
        _ = tx.SortedSetRemoveAsync(LastSeenKey, id);
        _ = tx.PublishAsync(RedisChannel.Literal(Channel), JsonSerializer.Serialize(new LiveMessage("offline", driverId), Json));
        await tx.ExecuteAsync();
    }

    /// <summary>Removes drivers not seen since <paramref name="cutoffMs"/> (server time). Returns how many.</summary>
    public async Task<int> SweepAsync(long cutoffMs) =>
        (int)await Db.ScriptEvaluateAsync(SweepScript, [GeoKey, LastSeenKey], [cutoffMs, Channel]);

    private async Task<IReadOnlyList<LivePosition>> LoadAsync(RedisValue[] members)
    {
        if (members.Length == 0) return [];
        var batch = Db.CreateBatch();
        var loads = members.Select(m => (Id: Guid.Parse(m.ToString()), Task: batch.HashGetAllAsync(DriverKey(Guid.Parse(m.ToString()))))).ToList();
        batch.Execute();
        var result = new List<LivePosition>(loads.Count);
        foreach (var (id, task) in loads)
            if (Parse(id, await task) is { } p) result.Add(p);
        return result;
    }

    private static LivePosition? Parse(Guid driverId, HashEntry[] entries)
    {
        if (entries.Length == 0) return null;
        var h = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
        if (!h.TryGetValue("lat", out var lat) || !h.TryGetValue("lon", out var lon) || !h.TryGetValue("ts", out var ts))
            return null;
        return new LivePosition(driverId, D(lat), D(lon),
            DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(ts, CultureInfo.InvariantCulture)),
            DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(h.GetValueOrDefault("rcv", ts), CultureInfo.InvariantCulture)),
            Opt(h, "spd"), Opt(h, "hdg"), Opt(h, "acc"),
            h.TryGetValue("veh", out var veh) ? Guid.Parse(veh) : null,
            h.TryGetValue("shp", out var shp) ? Guid.Parse(shp) : null,
            h.GetValueOrDefault("trk"));
    }

    private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    private static double? Opt(Dictionary<string, string> h, string key) => h.TryGetValue(key, out var v) ? D(v) : null;
}
