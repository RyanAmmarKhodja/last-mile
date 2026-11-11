using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using StackExchange.Redis;

namespace LastMile.Tracking.Services;

/// <summary>
/// One Redis subscription per replica to <c>tracking:positions</c>, fanned out to this replica's SSE connections (T2).
/// Every replica receives every message, so a client can be connected to any replica.
/// </summary>
public sealed class PositionHub(IConnectionMultiplexer redis, ILogger<PositionHub> logger) : IHostedService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<DriverSink, byte>> _byDriver = new();
    private readonly ConcurrentDictionary<FleetSink, byte> _fleet = new();
    private ChannelMessageQueue? _queue;

    private readonly CancellationTokenSource _stopping = new();

    public Task StartAsync(CancellationToken ct)
    {
        // Don't block startup on Redis (NFR-3): subscribe in the background, retrying until Redis is reachable.
        // Once subscribed, StackExchange.Redis re-subscribes by itself after reconnects.
        _ = Task.Run(async () =>
        {
            while (!_stopping.IsCancellationRequested)
            {
                try
                {
                    // OnMessage processes messages one at a time in publish order (per-driver order is preserved).
                    _queue = await redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(LivePositionStore.Channel));
                    _queue.OnMessage(m => Dispatch(m.Message));
                    logger.LogInformation("Subscribed to {Channel}", LivePositionStore.Channel);
                    return;
                }
                catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
                {
                    logger.LogWarning("Redis subscribe failed, retrying in 5 s: {Message}", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), _stopping.Token).ContinueWith(_ => { });
                }
            }
        });
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _stopping.CancelAsync();
        if (_queue is not null) await _queue.UnsubscribeAsync();
    }

    /// <summary>Messages for one driver (public tracking stream). Dispose to unsubscribe.</summary>
    public DriverSink SubscribeDriver(Guid driverId)
    {
        var sink = new DriverSink(this, driverId);
        _byDriver.GetOrAdd(driverId, _ => new()).TryAdd(sink, 0);
        return sink;
    }

    /// <summary>Latest message per driver, drained by the caller every interval (admin fleet stream).</summary>
    public FleetSink SubscribeFleet()
    {
        var sink = new FleetSink(this);
        _fleet.TryAdd(sink, 0);
        return sink;
    }

    internal void Remove(DriverSink sink)
    {
        if (!_byDriver.TryGetValue(sink.DriverId, out var sinks)) return;
        sinks.TryRemove(sink, out _);
        if (sinks.IsEmpty) _byDriver.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<DriverSink, byte>>(sink.DriverId, sinks));
    }

    internal void Remove(FleetSink sink) => _fleet.TryRemove(sink, out _);

    private void Dispatch(RedisValue raw)
    {
        LiveMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<LiveMessage>(raw.ToString(), LivePositionStore.Json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Ignoring malformed live message: {Message}", ex.Message);
            return;
        }
        if (msg is null) return;

        if (_byDriver.TryGetValue(msg.DriverId, out var sinks))
            foreach (var sink in sinks.Keys) sink.Push(msg);
        foreach (var sink in _fleet.Keys) sink.Push(msg);
    }
}

/// <summary>Bounded per-connection queue; a slow client loses the oldest positions, never blocks the hub.</summary>
public sealed class DriverSink : IDisposable
{
    private readonly PositionHub _hub;
    private readonly Channel<LiveMessage> _channel =
        Channel.CreateBounded<LiveMessage>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    internal DriverSink(PositionHub hub, Guid driverId)
    {
        _hub = hub;
        DriverId = driverId;
    }

    public Guid DriverId { get; }
    public ChannelReader<LiveMessage> Reader => _channel.Reader;
    internal void Push(LiveMessage msg) => _channel.Writer.TryWrite(msg);
    public void Dispose() => _hub.Remove(this);
}

/// <summary>Keeps only the latest message per driver between two drains (one batch per second at any ping rate).</summary>
public sealed class FleetSink : IDisposable
{
    private readonly PositionHub _hub;
    private ConcurrentDictionary<Guid, LiveMessage> _pending = new();

    internal FleetSink(PositionHub hub) => _hub = hub;

    internal void Push(LiveMessage msg) => _pending[msg.DriverId] = msg;

    public IReadOnlyCollection<LiveMessage> Drain() => Interlocked.Exchange(ref _pending, new()).Values.ToList();

    public void Dispose() => _hub.Remove(this);
}
