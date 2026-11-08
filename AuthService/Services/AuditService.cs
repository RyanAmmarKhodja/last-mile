using LastMile.Auth.Data;
using LastMile.Auth.Enums;
using LastMile.Auth.Models;

namespace LastMile.Auth.Services;

/// <summary>Records every action both to the audit_logs table and to the structured application log.</summary>
public sealed class AuditService(AuthDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger)
{
    public string? ClientIp => http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public async Task LogAsync(AuditAction action, bool success, string? actorId, string? subject = null,
        string? details = null, CancellationToken ct = default)
    {
        var ip = ClientIp;
        logger.Log(success ? LogLevel.Information : LogLevel.Warning,
            "AUDIT {Action} success={Success} actor={ActorId} subject={Subject} ip={IpAddress} details={Details}",
            action, success, actorId, subject, ip, details);

        db.AuditLogs.Add(new AuditLogEntity
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = action.ToString(),
            Success = success,
            ActorId = actorId,
            Subject = subject,
            IpAddress = ip,
            Details = details
        });
        await db.SaveChangesAsync(ct);
    }
}
