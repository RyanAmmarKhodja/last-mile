using System.ComponentModel.DataAnnotations;
using LastMile.Fleet.Core;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Models;

namespace LastMile.Fleet.DTOs;

/// <summary>The vehicle a driver currently operates.</summary>
public sealed record VehicleSummaryDto(Guid Id, string LicensePlate, string Model, decimal CapacityKg)
{
    public static VehicleSummaryDto? From(VehicleEntity? v) =>
        v is null ? null : new VehicleSummaryDto(v.Id, v.LicensePlate, v.Model, v.CapacityKg);
}

/// <summary>A delivery driver, with availability (D3: on shift and ready) and the vehicle they operate.</summary>
public sealed record DriverDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string LicenseNumber,
    string PhoneNumber,
    bool IsAvailable,
    DriverStatus Status,
    VehicleSummaryDto? Vehicle,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DriverDto From(Driver d, VehicleEntity? vehicle) => new(d.Id, d.UserId, d.FirstName, d.LastName,
        d.LicenseNumber, d.PhoneNumber, d.IsAvailable, d.Status, VehicleSummaryDto.From(vehicle), d.CreatedAt, d.UpdatedAt);
}

/// <summary>A delivery vehicle.</summary>
public sealed record VehicleDto(
    Guid Id,
    string LicensePlate,
    string Model,
    decimal CapacityKg,
    bool IsActive,
    Guid? AssignedDriverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static VehicleDto From(Vehicle v) =>
        new(v.Id, v.LicensePlate, v.Model, v.CapacityKg, v.IsActive, v.AssignedDriverId, v.CreatedAt, v.UpdatedAt);
}

/// <summary>Full replacement of a driver's editable fields (PUT). The license number is stored upper-case.</summary>
public sealed class UpdateDriverDto
{
    [Required, MaxLength(FleetRules.MaxNameLength)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(FleetRules.MaxNameLength)]
    public string LastName { get; init; } = string.Empty;

    [Required, MaxLength(FleetRules.MaxPhoneLength)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, MaxLength(FleetRules.MaxLicenseLength)]
    public string LicenseNumber { get; init; } = string.Empty;

    public DriverDetails ToDetails() => new(FirstName, LastName, PhoneNumber, LicenseNumber);
}

/// <summary>Start (<c>true</c>) or end (<c>false</c>) a shift.</summary>
public sealed class AvailabilityDto
{
    [Required]
    public bool? IsAvailable { get; init; }
}

/// <summary>Vehicle details (POST / full replacement on PUT). The plate is stored upper-case.</summary>
public sealed class VehicleDetailsDto
{
    [Required, MaxLength(FleetRules.MaxPlateLength)]
    public string LicensePlate { get; init; } = string.Empty;

    [Required, MaxLength(FleetRules.MaxModelLength)]
    public string Model { get; init; } = string.Empty;

    /// <summary>Payload capacity in kg, 0 &lt; x ≤ 100000.</summary>
    [Required, Range(0.01, 100_000)]
    public decimal? CapacityKg { get; init; }

    public VehicleDetails ToDetails() => new(LicensePlate, Model, CapacityKg);
}

/// <summary>Assign the vehicle to a driver, or unassign it with <c>null</c>.</summary>
public sealed class AssignmentDto
{
    public Guid? DriverId { get; init; }
}

/// <summary>Retire (<c>false</c>) or reactivate (<c>true</c>) a vehicle.</summary>
public sealed class VehicleStatusDto
{
    [Required]
    public bool? IsActive { get; init; }
}

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
