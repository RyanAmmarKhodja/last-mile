using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging.Inbox;

/// <summary>Purges inbox rows older than the topic retention. Older events can no longer be redelivered.</summary>
public sealed class InboxCleanupService<TDbContext>(
    IServiceScopeFactory scopes,
    IOptions<MessagingOptions> options,
    ILogger<InboxCleanupService<TDbContext>> logger) : BackgroundService where TDbContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var cutoff = DateTimeOffset.UtcNow - options.Value.Consumer.InboxRetention;
                var deleted = await db.Set<InboxMessage>().Where(m => m.ProcessedAt < cutoff).ExecuteDeleteAsync(ct);
                if (deleted > 0) logger.LogInformation("Purged {Count} inbox row(s)", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Inbox cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
