namespace LastMile.Auth.Models;

/// <summary>RSA key pair used to sign JWTs. Public halves are published through the JWKS endpoint.</summary>
public class SigningKeyEntity
{
    public string KeyId { get; set; } = null!;
    public string PrivateKeyPem { get; set; } = null!;
    public string Algorithm { get; set; } = "RS256";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
