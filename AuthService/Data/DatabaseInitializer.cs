using LastMile.Auth.Core;
using LastMile.Auth.Enums;
using LastMile.Auth.Options;
using LastMile.Auth.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Auth.Data;

public static class DatabaseInitializer
{
    /// <summary>Applies EF Core migrations, loads/creates signing keys and seeds bootstrap data.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await MigrateWithRetryAsync(db, logger, ct);
        }

        await app.Services.GetRequiredService<SigningKeyService>().InitializeAsync(ct);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
            await SeedAdminAsync(scope.ServiceProvider, seed, logger, ct);

            var clients = scope.ServiceProvider.GetRequiredService<ServiceClientService>();
            foreach (var c in seed.ServiceClients.Where(c => !string.IsNullOrWhiteSpace(c.ClientId)))
            {
                if (await clients.EnsureExistsAsync(c.ClientId, c.Name, c.ClientSecret, c.Scopes, c.Audiences, ct))
                    logger.LogInformation("Seeded service client {ClientId}", c.ClientId);
            }
        }
    }

    private static async Task MigrateWithRetryAsync(AuthDbContext db, ILogger logger, CancellationToken ct)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database schema is up to date");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is Npgsql.NpgsqlException or TimeoutException)
            {
                logger.LogWarning("Database not reachable (attempt {Attempt}/{Max}): {Message}",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider sp, SeedOptions seed, ILogger logger, CancellationToken ct)
    {
        if (seed.Admin is null || string.IsNullOrWhiteSpace(seed.Admin.Email)) return;

        var db = sp.GetRequiredService<AuthDbContext>();
        if (await db.Users.AnyAsync(u => u.Role == Roles.Admin, ct)) return;

        User.ValidatePasswordPolicy(seed.Admin.Password);
        var hasher = sp.GetRequiredService<PasswordHashingService>();
        var admin = User.Create(seed.Admin.Email, hasher.Hash(seed.Admin.Password), UserRole.ADMIN);
        db.Users.Add(admin.ToEntity());
        await db.SaveChangesAsync(ct);

        await sp.GetRequiredService<AuditService>()
            .LogAsync(AuditAction.UserCreated, true, "system", admin.Id.ToString(), $"seeded admin {admin.Email}", ct);
        logger.LogInformation("Seeded bootstrap administrator {Email}", admin.Email);
    }
}
