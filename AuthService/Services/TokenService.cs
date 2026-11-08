using System.Security.Cryptography;
using System.Text;
using LastMile.Auth.Core;
using LastMile.Auth.Enums;
using LastMile.Auth.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LastMile.Auth.Services;

public sealed record IssuedToken(string Token, string Jti, DateTimeOffset ExpiresAt, int ExpiresIn);

/// <summary>Cryptographic token generation: signed JWT access tokens and opaque refresh tokens / secrets.</summary>
public sealed class TokenService(SigningKeyService keys, IOptions<JwtOptions> options)
{
    private const int MaxM2MTokenMinutes = 240;
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly JwtOptions _options = options.Value;

    public IssuedToken CreateUserAccessToken(User user)
    {
        var claims = new Dictionary<string, object>
        {
            [TokenClaims.Subject] = user.Id.ToString(),
            [TokenClaims.Role] = user.Role.ToString(),
            [TokenClaims.Email] = user.Email
            // profile_id: profiles are owned by ProfileService / FleetService (database-per-service);
            // it will be added once a profile lookup (API call or Kafka-fed projection) is available.
        };
        return Create(claims, _options.Audience, _options.AccessTokenMinutes);
    }

    public IssuedToken CreateM2MToken(ServiceClient client, string audience, IReadOnlyList<string> scopes)
    {
        var claims = new Dictionary<string, object>
        {
            [TokenClaims.Subject] = client.Subject,
            [TokenClaims.ClientId] = client.ClientId,
            [TokenClaims.Scope] = string.Join(' ', scopes),
            [TokenClaims.GrantType] = TokenClaims.ClientCredentialsGrant
        };
        return Create(claims, audience, Math.Clamp(_options.M2MTokenMinutes, 1, MaxM2MTokenMinutes));
    }

    private IssuedToken Create(Dictionary<string, object> claims, string audience, int lifetimeMinutes)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(lifetimeMinutes);
        var jti = Guid.NewGuid().ToString();
        claims[TokenClaims.JwtId] = jti;

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = claims,
            SigningCredentials = keys.SigningCredentials
        });
        return new IssuedToken(token, jti, expires, lifetimeMinutes * 60);
    }

    /// <summary>512-bit random opaque token, base64url encoded.</summary>
    public static string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public static string GenerateClientSecret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>Refresh tokens are high-entropy, so a fast unsalted SHA-256 is sufficient for lookup-by-hash.</summary>
    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
