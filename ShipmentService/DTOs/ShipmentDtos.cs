using System.ComponentModel.DataAnnotations;
using LastMile.Shipments.Core;
using LastMile.Shipments.Enums;

namespace LastMile.Shipments.DTOs;

/// <summary>
/// POST /shipments (decision S1). A CLIENT is the sender and names the recipient by <see cref="RecipientEmail"/>.
/// An ADMIN must give <see cref="SenderClientId"/> and a recipient (client id or email).
/// Addresses default to the sender's and recipient's profile addresses.
/// </summary>
public sealed class CreateShipmentDto
{
    /// <summary>ADMIN only: the sending client's profile id.</summary>
    public Guid? SenderClientId { get; init; }

    /// <summary>ADMIN only: the recipient's client profile id (alternative to <see cref="RecipientEmail"/>).</summary>
    public Guid? RecipientClientId { get; init; }

    /// <summary>Email of the recipient's account (must be an active CLIENT).</summary>
    [EmailAddress, MaxLength(255)]
    public string? RecipientEmail { get; init; }

    [Required, Range(0.01, 100_000)]
    public decimal? WeightKg { get; init; }

    [MaxLength(ShipmentRules.MaxAddressLength)]
    public string? OriginAddress { get; init; }

    [MaxLength(ShipmentRules.MaxAddressLength)]
    public string? DestinationAddress { get; init; }

    /// <summary>Stored on the CREATED history row (visible to the parties, never on public tracking).</summary>
    [MaxLength(ShipmentRules.MaxNotesLength)]
    public string? Notes { get; init; }
}

/// <summary>POST …/dispatch. No driver = auto-pick the smallest available vehicle that fits (decision S2).</summary>
public sealed class DispatchDto
{
    public Guid? DriverId { get; init; }
}

/// <summary>Optional note (deliver = proof-of-delivery note, cancel = reason).</summary>
public sealed class NoteDto
{
    [MaxLength(ShipmentRules.MaxNotesLength)]
    public string? Notes { get; init; }
}

/// <summary>POST …/fail: the reason is required.</summary>
public sealed class ReasonDto
{
    [Required, MaxLength(ShipmentRules.MaxNotesLength)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record StatusEntryDto(ShipmentStatus Status, string? Notes, string ActorId, DateTimeOffset CreatedAt)
{
    public static StatusEntryDto From(StatusEntry h) => new(h.Status, h.Notes, h.ActorId, h.CreatedAt);
}

/// <summary>Full shipment with its audit trail (parties, admins, services).</summary>
public sealed record ShipmentDto(
    Guid Id,
    string TrackingNumber,
    ShipmentStatus Status,
    Guid SenderId,
    Guid SenderUserId,
    Guid RecipientId,
    Guid RecipientUserId,
    string OriginAddress,
    string DestinationAddress,
    decimal WeightKg,
    Guid? AssignedDriverId,
    Guid? AssignedVehicleId,
    IReadOnlyList<StatusEntryDto> History,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ShipmentDto From(Shipment s) => new(s.Id, s.TrackingNumber, s.Status, s.SenderId, s.SenderUserId,
        s.RecipientId, s.RecipientUserId, s.OriginAddress, s.DestinationAddress, s.WeightKg, s.AssignedDriverId,
        s.AssignedVehicleId, s.History.Select(StatusEntryDto.From).ToList(), s.CreatedAt, s.UpdatedAt);
}

/// <summary>List item (no history).</summary>
public sealed record ShipmentSummaryDto(
    Guid Id,
    string TrackingNumber,
    ShipmentStatus Status,
    Guid SenderId,
    Guid RecipientId,
    string DestinationAddress,
    decimal WeightKg,
    Guid? AssignedDriverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TimelineEntryDto(ShipmentStatus Status, DateTimeOffset At);

/// <summary>Public tracking view (decision S3): no names, addresses, phones or notes.</summary>
public sealed record TrackingDto(
    string TrackingNumber,
    ShipmentStatus Status,
    Guid? AssignedDriverId,
    Guid? AssignedVehicleId,
    IReadOnlyList<TimelineEntryDto> Timeline,
    DateTimeOffset UpdatedAt);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
