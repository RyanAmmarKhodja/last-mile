namespace LastMile.Shipments.Services.Clients;

// Read models of other services' APIs: only the fields ShipmentService needs. Unknown fields are ignored.

/// <summary>AuthService <c>GET /api/v1/users/lookup</c>.</summary>
public sealed record UserLookup(Guid Id, string Role, bool IsActive);

/// <summary>ProfileService <c>ClientProfileDto</c>.</summary>
public sealed record ClientProfile(Guid Id, Guid UserId, string FirstName, string LastName, string Address, string Status)
{
    public bool IsActive => Status == "ACTIVE";
}

/// <summary>FleetService <c>VehicleSummaryDto</c>.</summary>
public sealed record FleetVehicle(Guid Id, string LicensePlate, decimal CapacityKg);

/// <summary>FleetService <c>DriverDto</c>.</summary>
public sealed record FleetDriver(Guid Id, Guid UserId, bool IsAvailable, string Status, FleetVehicle? Vehicle)
{
    public bool IsActive => Status == "ACTIVE";
}

public sealed record RemotePage<T>(IReadOnlyList<T> Items, int TotalCount);
