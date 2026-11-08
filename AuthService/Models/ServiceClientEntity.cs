namespace LastMile.Auth.Models;

/// <summary>Persistence model for M2M service accounts ("service_clients" table).</summary>
public class ServiceClientEntity
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string SecretHash { get; set; } = null!;
    public List<string> AllowedScopes { get; set; } = [];
    public List<string> AllowedAudiences { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
