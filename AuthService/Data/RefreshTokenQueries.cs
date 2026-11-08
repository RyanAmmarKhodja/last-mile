using Microsoft.EntityFrameworkCore;

namespace LastMile.Auth.Data;

public static class RefreshTokenQueries
{
    /// <summary>Revokes every still-active refresh token of a user (password change, deactivation, reuse detection...).</summary>
    public static Task<int> RevokeAllRefreshTokensAsync(this AuthDbContext db, Guid userId, string reason,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason), ct);
    }
}
