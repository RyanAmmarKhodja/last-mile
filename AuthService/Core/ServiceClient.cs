namespace LastMile.Auth.Core;

/// <summary>
/// A machine-to-machine principal authenticating via the OAuth 2.0 client_credentials grant.
/// </summary>
public sealed class ServiceClient
{
    public Guid Id { get; }
    public string ClientId { get; }
    public string Name { get; }
    public string SecretHash { get; private set; }
    public IReadOnlyList<string> AllowedScopes { get; }
    public IReadOnlyList<string> AllowedAudiences { get; }
    public bool IsActive { get; }
    public DateTimeOffset CreatedAt { get; }

    private ServiceClient(Guid id, string clientId, string name, string secretHash,
        IReadOnlyList<string> scopes, IReadOnlyList<string> audiences, bool isActive, DateTimeOffset createdAt)
    {
        Id = id;
        ClientId = clientId;
        Name = name;
        SecretHash = secretHash;
        AllowedScopes = scopes;
        AllowedAudiences = audiences;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public static ServiceClient Create(string clientId, string name, string secretHash,
        IEnumerable<string> scopes, IEnumerable<string> audiences)
    {
        clientId = (clientId ?? string.Empty).Trim();
        if (clientId.Length is < 3 or > 100 || !clientId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            throw new DomainValidationException(
                "client_id must be 3-100 characters of letters, digits, '-', '_' or '.'.");

        var scopeList = Clean(scopes);
        var audienceList = Clean(audiences);
        if (audienceList.Count == 0)
            throw new DomainValidationException("At least one allowed audience is required.");
        if (scopeList.Any(s => s.Any(char.IsWhiteSpace)) || audienceList.Any(a => a.Any(char.IsWhiteSpace)))
            throw new DomainValidationException("Scopes and audiences cannot contain whitespace.");

        return new ServiceClient(Guid.CreateVersion7(), clientId, string.IsNullOrWhiteSpace(name) ? clientId : name.Trim(),
            secretHash, scopeList, audienceList, true, DateTimeOffset.UtcNow);
    }

    public static ServiceClient Rehydrate(Guid id, string clientId, string name, string secretHash,
        IReadOnlyList<string> scopes, IReadOnlyList<string> audiences, bool isActive, DateTimeOffset createdAt) =>
        new(id, clientId, name, secretHash, scopes, audiences, isActive, createdAt);

    /// <summary>Principal name used as the token subject, e.g. "shipment-service@clients".</summary>
    public string Subject => $"{ClientId}@clients";

    public void ChangeSecretHash(string secretHash) => SecretHash = secretHash;

    /// <summary>
    /// Returns the granted scopes. When none are requested, all allowed scopes are granted;
    /// requesting a scope outside the allow-list is an error.
    /// </summary>
    public IReadOnlyList<string> ResolveScopes(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return AllowedScopes;
        var scopes = requested.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        var denied = scopes.Except(AllowedScopes).ToList();
        if (denied.Count > 0)
            throw OAuthException.InvalidScope($"Scope(s) not allowed for this client: {string.Join(' ', denied)}");
        return scopes;
    }

    /// <summary>Returns the audience for the token; defaults to the only allowed audience when unambiguous.</summary>
    public string ResolveAudience(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            if (AllowedAudiences.Count == 1) return AllowedAudiences[0];
            throw OAuthException.InvalidRequest(
                "The 'audience' parameter is required because this client may call several services.");
        }
        if (!AllowedAudiences.Contains(requested.Trim()))
            throw OAuthException.InvalidTarget($"Audience '{requested}' is not allowed for this client.");
        return requested.Trim();
    }

    private static List<string> Clean(IEnumerable<string>? values) =>
        (values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();
}
