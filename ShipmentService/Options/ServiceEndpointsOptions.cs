namespace LastMile.Shipments.Options;

/// <summary>
/// Downstream services called with M2M tokens (section <c>Services</c>). ShipmentService authenticates as the
/// <see cref="ClientId"/> service client (OAuth client credentials against AuthService).
/// </summary>
public sealed class ServiceEndpointsOptions
{
    public const string Section = "Services";

    public string AuthBaseUrl { get; set; } = "http://auth-service:8080";
    public string ProfileBaseUrl { get; set; } = "http://profile-service:8080";
    public string FleetBaseUrl { get; set; } = "http://fleet-service:8080";

    public string ClientId { get; set; } = "shipment-service";
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Per-request timeout. A slow dependency fails fast with 503 instead of holding the request.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
