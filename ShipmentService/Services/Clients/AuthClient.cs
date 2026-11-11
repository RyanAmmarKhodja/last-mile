namespace LastMile.Shipments.Services.Clients;

/// <summary>AuthService, M2M audience <c>auth-service</c>, scope <c>users:lookup</c>.</summary>
public sealed class AuthClient(HttpClient http)
{
    /// <summary>The account with this email, or null (decision S1).</summary>
    public Task<UserLookup?> LookupUserAsync(string email, CancellationToken ct) =>
        RemoteCall.GetAsync<UserLookup>(http, "AuthService", $"api/v1/users/lookup?email={Uri.EscapeDataString(email)}", ct);
}
