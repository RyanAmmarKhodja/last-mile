using System.ComponentModel.DataAnnotations;
using LastMile.Profile.Core;
using LastMile.Profile.Enums;

namespace LastMile.Profile.DTOs;

/// <summary>A client (shipment sender / recipient) profile.</summary>
public sealed record ClientProfileDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string? CompanyName,
    string PhoneNumber,
    string Address,
    ProfileStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ClientProfileDto From(ClientProfile p) => new(p.Id, p.UserId, p.FirstName, p.LastName, p.CompanyName,
        p.PhoneNumber, p.Address, p.Status, p.CreatedAt, p.UpdatedAt);
}

/// <summary>An administrator profile.</summary>
public sealed record AdminProfileDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    ProfileStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AdminProfileDto From(AdminProfile p) =>
        new(p.Id, p.UserId, p.FirstName, p.LastName, p.PhoneNumber, p.Status, p.CreatedAt, p.UpdatedAt);
}

/// <summary>Full replacement of a client's editable fields (PUT). Values are trimmed; empty optional = null.</summary>
public sealed class UpdateClientProfileDto
{
    [Required, MaxLength(ProfileRules.MaxNameLength)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(ProfileRules.MaxNameLength)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(ProfileRules.MaxCompanyLength)]
    public string? CompanyName { get; init; }

    [Required, MaxLength(ProfileRules.MaxPhoneLength)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, MaxLength(ProfileRules.MaxAddressLength)]
    public string Address { get; init; } = string.Empty;

    public ClientDetails ToDetails() => new(FirstName, LastName, CompanyName, PhoneNumber, Address);
}

/// <summary>Full replacement of an administrator's editable fields (PUT).</summary>
public sealed class UpdateAdminProfileDto
{
    [Required, MaxLength(ProfileRules.MaxNameLength)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(ProfileRules.MaxNameLength)]
    public string LastName { get; init; } = string.Empty;

    [MaxLength(ProfileRules.MaxPhoneLength)]
    public string? PhoneNumber { get; init; }

    public AdminDetails ToDetails() => new(FirstName, LastName, PhoneNumber);
}

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
