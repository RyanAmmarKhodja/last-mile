namespace LastMile.Auth.Enums;

/// <summary>Claim names used in issued tokens (see AuthService spec, "Token Claims").</summary>
public static class TokenClaims
{
    public const string Subject = "sub";
    public const string JwtId = "jti";
    public const string Expires = "exp";
    public const string Role = "role";
    public const string Email = "email";
    public const string ProfileId = "profile_id";
    public const string ClientId = "client_id";
    public const string Scope = "scope";
    public const string GrantType = "gty";

    public const string ClientCredentialsGrant = "client_credentials";
}
