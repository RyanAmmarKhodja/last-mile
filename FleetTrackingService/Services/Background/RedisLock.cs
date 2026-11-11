using StackExchange.Redis;

namespace LastMile.Tracking.Services.Background;

/// <summary>
/// Lease lock (<c>SET key owner NX PX lease</c>) so that only one replica runs a periodic job (NFR-4). The lease is not
/// released early: it expires shortly before the next tick, which also prevents two replicas running the same tick.
/// </summary>
public sealed class RedisLock(IConnectionMultiplexer redis)
{
    private readonly string _owner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public Task<bool> TryAcquireAsync(string job, TimeSpan lease) =>
        redis.GetDatabase().StringSetAsync($"tracking:lock:{job}", _owner, lease, When.NotExists);
}

/// <summary>Base for the periodic jobs: runs <see cref="RunOnceAsync"/> every interval and logs (never crashes) on failure.</summary>
public abstract class PeriodicJob(ILogger logger) : BackgroundService
{
    protected abstract TimeSpan Interval { get; }
    protected virtual bool RunAtStartup => false;
    protected abstract Task RunOnceAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (RunAtStartup) await SafeRunAsync(ct);
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct)) await SafeRunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task SafeRunAsync(CancellationToken ct)
    {
        try
        {
            await RunOnceAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Job} failed; retrying on the next tick", GetType().Name);
        }
    }
}
