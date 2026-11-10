namespace LastMile.Profile.Enums;

/// <summary>Claim names in tokens issued by AuthService (documents/AuthService/api-reference.md §5).</summary>
public static class TokenClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Email = "email";
    public const string ClientId = "client_id";
    public const string Scope = "scope";
}
