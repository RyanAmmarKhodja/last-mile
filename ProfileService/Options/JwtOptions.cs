namespace LastMile.Profile.Options;

/// <summary>
/// Validation of tokens issued by AuthService. Signing keys are discovered from <see cref="MetadataAddress"/>
/// (OIDC discovery → jwks_uri) and cached; an unknown <c>kid</c> triggers a refresh, so key rotation works.
/// </summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>e.g. <c>http://auth-service:8080/.well-known/openid-configuration</c>.</summary>
    public string MetadataAddress { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    /// <summary>User tokens carry <c>last-mile-api</c>; M2M tokens requested for this service carry <c>profile-service</c>.</summary>
    public List<string> Audiences { get; set; } = [];
}
