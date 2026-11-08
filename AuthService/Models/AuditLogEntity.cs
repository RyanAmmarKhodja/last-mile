namespace LastMile.Auth.Models;

/// <summary>Append-only record of every security-relevant action performed by the service.</summary>
public class AuditLogEntity
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Action { get; set; } = null!;
    public bool Success { get; set; }
    public string? ActorId { get; set; }
    public string? Subject { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
}
