using LastMile.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging.Outbox;

/// <summary>
/// Degraded when the oldest unpublished event is older than <see cref="OutboxOptions.HealthMaxLag"/>, which
/// means Kafka or the Schema Registry is unreachable. The service keeps working because events wait in the
/// outbox, so this is never reported as Unhealthy.
/// </summary>
public sealed class OutboxHealthCheck<TDbContext>(TDbContext db, IOptions<MessagingOptions> options) : IHealthCheck
    where TDbContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var pending = db.Set<OutboxMessage>().Where(m => m.PublishedAt == null);
        var oldest = await pending.OrderBy(m => m.Id).Select(m => (DateTimeOffset?)m.OccurredAt).FirstOrDefaultAsync(ct);
        if (oldest is null) return HealthCheckResult.Healthy("Outbox is empty.");

        var lag = DateTimeOffset.UtcNow - oldest.Value;
        var count = await pending.CountAsync(ct);
        var data = new Dictionary<string, object> { ["pending"] = count, ["oldestAgeSeconds"] = (int)lag.TotalSeconds };
        return lag > options.Value.Outbox.HealthMaxLag
            ? HealthCheckResult.Degraded($"{count} event(s) waiting, oldest {lag.TotalSeconds:F0}s.", data: data)
            : HealthCheckResult.Healthy($"{count} event(s) waiting.", data);
    }
}
