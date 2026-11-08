using LastMile.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Auth.Services;

/// <summary>
/// Access-token blacklist keyed by jti. Backed by PostgreSQL for now; the interface lets a
/// Redis implementation (planned in the spec) be swapped in without touching callers.
/// </summary>
public interface ITokenBlacklist
{
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
    Task RevokeAsync(string jti, string subject, DateTimeOffset expiresAt, string reason, CancellationToken ct = default);
}

public sealed class TokenBlacklistService(AuthDbContext db) : ITokenBlacklist
{
    public Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default) =>
        db.RevokedTokens.AsNoTracking().AnyAsync(t => t.Jti == jti, ct);

    public async Task RevokeAsync(string jti, string subject, DateTimeOffset expiresAt, string reason,
        CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO revoked_tokens (jti, subject, expires_at, revoked_at, reason)
            VALUES ({jti}, {subject}, {expiresAt}, {DateTimeOffset.UtcNow}, {reason})
            ON CONFLICT (jti) DO NOTHING
            """, ct);
    }
}
