using LastMile.Tracking.Options;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Services.Background;

/// <summary>
/// Removes drivers without a ping for 10 minutes from the GEO and last-seen sets (FR-3, PRD retention). Their hash has
/// already expired by TTL; the sweep keeps the GEO index and the fleet list in step and tells the streams.
/// </summary>
public sealed class LiveSweeper(
    LivePositionStore live,
    RedisLock locks,
    IOptions<TrackingOptions> options,
    TimeProvider clock,
    ILogger<LiveSweeper> logger) : PeriodicJob(logger)
{
    protected override TimeSpan Interval => options.Value.SweepInterval;

    protected override async Task RunOnceAsync(CancellationToken ct)
    {
        if (!await locks.TryAcquireAsync("sweeper", Interval * 0.9)) return;
        var cutoff = (clock.GetUtcNow() - options.Value.LiveTtl).ToUnixTimeMilliseconds();
        var removed = await live.SweepAsync(cutoff);
        if (removed > 0) logger.LogInformation("Swept {Count} inactive drivers off the live map", removed);
    }
}
