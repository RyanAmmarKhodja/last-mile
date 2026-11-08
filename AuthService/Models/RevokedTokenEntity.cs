namespace LastMile.Auth.Models;

/// <summary>Blacklisted access token, keyed by its jti. Rows are purged once the token would have expired anyway.</summary>
public class RevokedTokenEntity
{
    public string Jti { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
    public string? Reason { get; set; }
}
