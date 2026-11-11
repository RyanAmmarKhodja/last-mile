namespace LastMile.Tracking.Options;

/// <summary>Configuration section <c>Tracking</c> (FleetTrackingService.md §9).</summary>
public sealed class TrackingOptions
{
    public const string Section = "Tracking";

    /// <summary>A driver without pings disappears from the live map after this (PRD retention: 10 min).</summary>
    public TimeSpan LiveTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>A ping timestamp may be at most this far in the future (device clock skew).</summary>
    public TimeSpan MaxFutureSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Older pings are rejected as stale: they would be invisible live and are never stored.</summary>
    public TimeSpan MaxPingAge { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Cold storage: one sample per driver per interval (T6).</summary>
    public TimeSpan SampleInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How often expired drivers are removed from the GEO set.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Partition maintenance and purge (FR-7).</summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>History retention in days (T5).</summary>
    public int HistoryRetentionDays { get; set; } = 30;

    /// <summary>Daily partitions created ahead of today.</summary>
    public int PartitionsAhead { get; set; } = 3;

    public int MaxBatchSize { get; set; } = 500;

    /// <summary>Per-replica cache of the driver context used on every ping.</summary>
    public int ContextCacheSeconds { get; set; } = 5;

    /// <summary>Admin fleet stream: one coalesced batch per interval.</summary>
    public TimeSpan FleetStreamInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>SSE heartbeat; the public stream also re-checks the shipment status at this pace.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
}
