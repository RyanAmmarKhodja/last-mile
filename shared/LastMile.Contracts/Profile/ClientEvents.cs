namespace LastMile.Contracts.Profile;

// Topic profile.client.v1: owner ProfileService, key = clientId.
// Contract: documents/ProfileService/ProfileService.md §8, documents/kafka-architecture.md §3.

/// <summary>A client profile was created (from <c>UserRegistered</c> or by the client's first <c>PUT /me</c>).</summary>
[IntegrationEvent(Topics.ProfileClientV1, "ClientCreated")]
public sealed record ClientCreated(
    Guid ClientId,
    Guid UserId,
    string FirstName,
    string LastName,
    string? CompanyName,
    string PhoneNumber,
    string Address);

/// <summary>A client profile's details changed. Carries the full current state.</summary>
[IntegrationEvent(Topics.ProfileClientV1, "ClientUpdated")]
public sealed record ClientUpdated(
    Guid ClientId,
    Guid UserId,
    string FirstName,
    string LastName,
    string? CompanyName,
    string PhoneNumber,
    string Address);

/// <summary>The client can no longer act (account deactivated or role changed away from CLIENT).</summary>
[IntegrationEvent(Topics.ProfileClientV1, "ClientDeactivated")]
public sealed record ClientDeactivated(Guid ClientId, Guid UserId);

/// <summary>A deactivated client is active again.</summary>
[IntegrationEvent(Topics.ProfileClientV1, "ClientReactivated")]
public sealed record ClientReactivated(Guid ClientId, Guid UserId);

/// <summary>The client's account was deleted. The profile row is kept (soft delete) for shipment history.</summary>
[IntegrationEvent(Topics.ProfileClientV1, "ClientDeleted")]
public sealed record ClientDeleted(Guid ClientId, Guid UserId);
