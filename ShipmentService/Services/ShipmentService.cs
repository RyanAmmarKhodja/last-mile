using System.Security.Claims;
using LastMile.Shipments.Core;
using LastMile.Shipments.Data;
using LastMile.Shipments.DTOs;
using LastMile.Shipments.Enums;
using LastMile.Shipments.Models;
using LastMile.Shipments.Services.Clients;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Shipments.Services;

/// <summary>
/// Shipment use cases: creation (decision S1), reads with ownership rules, driver progress, cancellation and public
/// tracking (S3). Dispatch lives in <see cref="DispatchService"/>.
/// </summary>
public sealed class ShipmentService(
    ShipmentDbContext db,
    AuthClient auth,
    ProfileClient profiles,
    ShipmentEventPublisher events,
    ILogger<ShipmentService> logger)
{
    public const int MaxPageSize = 100;

    // ---- Create (S1) ---------------------------------------------------------------------------

    public async Task<ShipmentDto> CreateAsync(CreateShipmentDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var isAdmin = actor.IsAdmin();
        if (!isAdmin && (dto.SenderClientId is not null || dto.RecipientClientId is not null))
            throw new DomainValidationException("Clients ship as themselves and name the recipient by recipientEmail.");

        var sender = isAdmin
            ? await profiles.GetClientAsync(dto.SenderClientId ?? throw new DomainValidationException("senderClientId is required."), ct)
              ?? throw new NotFoundException("Sender client profile not found.")
            : await profiles.GetClientByUserAsync(actor.GetUserId(), ct)
              ?? throw new NotFoundException("You have no client profile yet; create it with PUT /api/v1/profiles/clients/me.");
        if (!sender.IsActive) throw new ConflictException($"The sender's client profile is {sender.Status}.");

        var recipient = await ResolveRecipientAsync(dto, isAdmin, ct);

        var shipment = Shipment.Create(
            new ShipmentParties(sender.Id, sender.UserId, recipient.Id, recipient.UserId),
            string.IsNullOrWhiteSpace(dto.OriginAddress) ? sender.Address : dto.OriginAddress,
            string.IsNullOrWhiteSpace(dto.DestinationAddress) ? recipient.Address : dto.DestinationAddress,
            dto.WeightKg, actor.ActorId(), dto.Notes);

        db.Shipments.Add(shipment.ToEntity());
        events.Created(shipment); // same transaction as the shipment and its CREATED row
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Shipment {TrackingNumber} ({ShipmentId}) created by {Actor}", shipment.TrackingNumber,
            shipment.Id, actor.ActorId());
        return ShipmentDto.From(shipment);
    }

    private async Task<ClientProfile> ResolveRecipientAsync(CreateShipmentDto dto, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin && dto.RecipientClientId is { } recipientId)
        {
            var byId = await profiles.GetClientAsync(recipientId, ct) ?? throw new NotFoundException("Recipient client profile not found.");
            return byId.IsActive ? byId : throw new ConflictException($"The recipient's client profile is {byId.Status}.");
        }

        if (string.IsNullOrWhiteSpace(dto.RecipientEmail))
            throw new DomainValidationException(isAdmin ? "recipientClientId or recipientEmail is required." : "recipientEmail is required.");

        var user = await auth.LookupUserAsync(dto.RecipientEmail, ct)
                   ?? throw new NotFoundException("No account exists with the recipient's email. Recipients must be registered clients.");
        if (user.Role != Roles.Client) throw new ConflictException("The recipient must be a CLIENT account.");
        if (!user.IsActive) throw new ConflictException("The recipient's account is disabled.");

        var recipient = await profiles.GetClientByUserAsync(user.Id, ct)
                        ?? throw new ConflictException("The recipient has no client profile yet; try again shortly.");
        return recipient.IsActive ? recipient : throw new ConflictException($"The recipient's client profile is {recipient.Status}.");
    }

    // ---- Read ------------------------------------------------------------------------------------

    /// <summary>ADMIN / shipments:read see all; a CLIENT sees shipments they send or receive; a DRIVER those assigned to them.</summary>
    public async Task<PagedResultDto<ShipmentSummaryDto>> ListAsync(int page, int pageSize, ShipmentStatus? status,
        string? asParty, Guid? driverId, string? search, ClaimsPrincipal actor, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = db.Shipments.AsNoTracking();

        if (!actor.CanReadAllShipments())
        {
            var userId = actor.GetUserId();
            if (actor.IsInRole(Roles.Driver))
                query = query.Where(s => s.AssignedDriverUserId == userId);
            else if (actor.IsInRole(Roles.Client))
                query = asParty?.ToLowerInvariant() switch
                {
                    "sender" => query.Where(s => s.SenderUserId == userId),
                    "recipient" => query.Where(s => s.RecipientUserId == userId),
                    null or "" => query.Where(s => s.SenderUserId == userId || s.RecipientUserId == userId),
                    _ => throw new DomainValidationException("as must be 'sender' or 'recipient'.")
                };
            else throw new ForbiddenException("You cannot list shipments.");
        }

        if (status is not null)
        {
            var s = status.Value.ToString();
            query = query.Where(x => x.CurrentStatus == s);
        }
        if (driverId is not null) query = query.Where(x => x.AssignedDriverId == driverId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = TrackingNumber.Normalize(search);
            query = query.Where(x => x.TrackingNumber.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ShipmentSummaryDto(x.Id, x.TrackingNumber, Enum.Parse<ShipmentStatus>(x.CurrentStatus),
                x.SenderId, x.RecipientId, x.DestinationAddress, x.WeightKg, x.AssignedDriverId, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);
        return new PagedResultDto<ShipmentSummaryDto>(items, page, pageSize, total);
    }

    public async Task<ShipmentDto> GetAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await LoadAsync(id, tracking: false, ct);
        var shipment = entity.ToDomain();
        if (!actor.CanReadAllShipments())
        {
            var userId = actor.GetUserId();
            if (userId != shipment.SenderUserId && userId != shipment.RecipientUserId && userId != shipment.AssignedDriverUserId)
                throw new ForbiddenException("You can only access shipments you send, receive or carry.");
        }
        return ShipmentDto.From(shipment);
    }

    /// <summary>Public tracking (S3): status and timeline only.</summary>
    public async Task<TrackingDto> TrackAsync(string trackingNumber, CancellationToken ct)
    {
        var number = TrackingNumber.Normalize(trackingNumber);
        var entity = await db.Shipments.AsNoTracking().Include(s => s.StatusHistory)
                         .FirstOrDefaultAsync(s => s.TrackingNumber == number, ct)
                     ?? throw new NotFoundException("No shipment with this tracking number.");
        var s = entity.ToDomain();
        return new TrackingDto(s.TrackingNumber, s.Status, s.AssignedDriverId, s.AssignedVehicleId,
            s.History.Select(h => new TimelineEntryDto(h.Status, h.CreatedAt)).ToList(), s.UpdatedAt);
    }

    // ---- Transitions -----------------------------------------------------------------------------

    public Task<ShipmentDto> PickupAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct) =>
        DriverTransitionAsync(id, actor, s => s.Pickup(actor.ActorId()), ct);

    public Task<ShipmentDto> DeliverAsync(Guid id, string? notes, ClaimsPrincipal actor, CancellationToken ct) =>
        DriverTransitionAsync(id, actor, s => s.Deliver(actor.ActorId(), notes), ct);

    public Task<ShipmentDto> FailAsync(Guid id, string reason, ClaimsPrincipal actor, CancellationToken ct) =>
        DriverTransitionAsync(id, actor, s => s.Fail(actor.ActorId(), reason), ct);

    /// <summary>ADMIN: any non-final shipment. Sender: only while CREATED (enforced by the aggregate).</summary>
    public async Task<ShipmentDto> CancelAsync(Guid id, string? reason, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await LoadAsync(id, tracking: true, ct);
        var shipment = entity.ToDomain();
        var isAdmin = actor.IsAdmin();
        if (!isAdmin && actor.TryGetUserId() != shipment.SenderUserId)
            throw new ForbiddenException("Only the sender or an administrator can cancel a shipment.");
        return await ApplyAsync(entity, shipment, shipment.Cancel(actor.ActorId(), reason, isAdmin), actor, ct);
    }

    /// <summary>Pick-up / deliver / fail: the assigned driver or an ADMIN.</summary>
    private async Task<ShipmentDto> DriverTransitionAsync(Guid id, ClaimsPrincipal actor, Func<Shipment, StatusEntry> transition,
        CancellationToken ct)
    {
        var entity = await LoadAsync(id, tracking: true, ct);
        var shipment = entity.ToDomain();
        if (!actor.IsAdmin() && (shipment.AssignedDriverUserId is null || actor.TryGetUserId() != shipment.AssignedDriverUserId))
            throw new ForbiddenException("Only the assigned driver or an administrator can update this shipment.");
        return await ApplyAsync(entity, shipment, transition(shipment), actor, ct);
    }

    /// <summary>Persists a transition: status + new history row + outbox event in one SaveChanges.</summary>
    internal async Task<ShipmentDto> ApplyAsync(ShipmentEntity entity, Shipment shipment, StatusEntry entry,
        ClaimsPrincipal actor, CancellationToken ct)
    {
        shipment.ApplyTo(entity);
        db.StatusHistory.Add(entry.ToEntity(shipment.Id));
        events.Transitioned(shipment, entry);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Shipment {TrackingNumber} → {Status} by {Actor}", shipment.TrackingNumber, entry.Status, actor.ActorId());
        return ShipmentDto.From(shipment);
    }

    internal async Task<ShipmentEntity> LoadAsync(Guid id, bool tracking, CancellationToken ct)
    {
        var query = db.Shipments.Include(s => s.StatusHistory).AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Shipment not found.");
    }
}
