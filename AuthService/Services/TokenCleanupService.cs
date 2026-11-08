using LastMile.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Auth.Services;

/// <summary>Hourly purge of blacklist entries and refresh tokens that can no longer be used.</summary>
public sealed class TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenRetention = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                var now = DateTimeOffset.UtcNow;
                var cutoff = now - RefreshTokenRetention; // keep recent ones around for reuse detection / audit

                var blacklist = await db.RevokedTokens.Where(t => t.ExpiresAt < now).ExecuteDeleteAsync(stoppingToken);
                var refresh = await db.RefreshTokens.Where(t => t.ExpiresAt < cutoff).ExecuteDeleteAsync(stoppingToken);
                if (blacklist + refresh > 0)
                    logger.LogInformation("Token cleanup removed {Blacklist} blacklist entries and {Refresh} refresh tokens",
                        blacklist, refresh);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Token cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
