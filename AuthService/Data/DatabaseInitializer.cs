using LastMile.Auth.Core;
using LastMile.Auth.DTOs;
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
            await SeedUsersAsync(scope.ServiceProvider, seed, logger, ct);

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

    private static async Task SeedUsersAsync(IServiceProvider sp, SeedOptions seed, ILogger logger, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AuthDbContext>();
        var hasher = sp.GetRequiredService<PasswordHashingService>();
        var events = sp.GetRequiredService<UserEventPublisher>();
        var audit = sp.GetRequiredService<AuditService>();

        foreach (var u in seed.Users.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            var email = User.NormalizeEmail(u.Email);
            if (await db.Users.AnyAsync(x => x.Email == email, ct)) continue;

            User.ValidatePasswordPolicy(u.Password);
            var profile = RegistrationProfileRules.Validate(u.Role, u.Profile);
            var user = User.Create(email, hasher.Hash(u.Password), u.Role);
            db.Users.Add(user.ToEntity());
            events.Registered(user, profile); // same transaction as the user row
            await db.SaveChangesAsync(ct);

            await audit.LogAsync(AuditAction.UserCreated, true, "system", user.Id.ToString(),
                $"seeded {user.Role} {user.Email}", ct);
            logger.LogInformation("Seeded demo {Role} account {Email}", user.Role, user.Email);
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
        var profile = RegistrationProfileRules.Validate(UserRole.ADMIN,
            new RegistrationProfileDto { FirstName = seed.Admin.FirstName, LastName = seed.Admin.LastName });
        db.Users.Add(admin.ToEntity());
        sp.GetRequiredService<UserEventPublisher>().Registered(admin, profile);
        await db.SaveChangesAsync(ct);

        await sp.GetRequiredService<AuditService>()
            .LogAsync(AuditAction.UserCreated, true, "system", admin.Id.ToString(), $"seeded admin {admin.Email}", ct);
        logger.LogInformation("Seeded bootstrap administrator {Email}", admin.Email);
    }
}
