using LastMile.Shipments.Enums;

namespace LastMile.Shipments.Core;

/// <summary>One row of the append-only status history (the shipment audit trail).</summary>
public sealed record StatusEntry(Guid Id, ShipmentStatus Status, string? Notes, string ActorId, DateTimeOffset CreatedAt);

/// <summary>Who sends to whom: client profile ids plus their AuthService user ids (for ownership checks).</summary>
public sealed record ShipmentParties(Guid SenderId, Guid SenderUserId, Guid RecipientId, Guid RecipientUserId);

/// <summary>
/// The shipment aggregate: snapshots and the state machine
/// CREATED → DISPATCHED → IN_TRANSIT → DELIVERED | FAILED, with CANCELLED from any non-final state.
/// Every transition appends exactly one <see cref="StatusEntry"/>; <see cref="Status"/> always equals the latest one.
/// </summary>
public sealed class Shipment
{
    private readonly List<StatusEntry> _history;

    public Guid Id { get; }
    public string TrackingNumber { get; }
    public Guid SenderId { get; }
    public Guid SenderUserId { get; }
    public Guid RecipientId { get; }
    public Guid RecipientUserId { get; }
    public Guid? AssignedDriverId { get; private set; }
    public Guid? AssignedDriverUserId { get; private set; }
    public Guid? AssignedVehicleId { get; private set; }
    public string OriginAddress { get; }
    public string DestinationAddress { get; }
    public decimal WeightKg { get; }
    public ShipmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<StatusEntry> History => _history;

    public bool IsFinal => Status is ShipmentStatus.DELIVERED or ShipmentStatus.FAILED or ShipmentStatus.CANCELLED;

    private Shipment(Guid id, string trackingNumber, ShipmentParties parties, string origin, string destination,
        decimal weightKg, ShipmentStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt, List<StatusEntry> history)
    {
        Id = id;
        TrackingNumber = trackingNumber;
        SenderId = parties.SenderId;
        SenderUserId = parties.SenderUserId;
        RecipientId = parties.RecipientId;
        RecipientUserId = parties.RecipientUserId;
        OriginAddress = origin;
        DestinationAddress = destination;
        WeightKg = weightKg;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _history = history;
    }

    public static Shipment Create(ShipmentParties parties, string? originAddress, string? destinationAddress,
        decimal? weightKg, string actorId, string? notes)
    {
        if (parties.SenderId == parties.RecipientId)
            throw new DomainValidationException("The sender and the recipient must be different clients.");
        var now = DateTimeOffset.UtcNow;
        var shipment = new Shipment(Guid.CreateVersion7(), Core.TrackingNumber.New(), parties,
            ShipmentRules.Address(originAddress, "Origin address"),
            ShipmentRules.Address(destinationAddress, "Destination address"),
            ShipmentRules.Weight(weightKg), ShipmentStatus.CREATED, now, now, []);
        shipment._history.Add(new StatusEntry(Guid.CreateVersion7(), ShipmentStatus.CREATED, ShipmentRules.Notes(notes), actorId, now));
        return shipment;
    }

    public static Shipment Rehydrate(Guid id, string trackingNumber, ShipmentParties parties, Guid? driverId,
        Guid? driverUserId, Guid? vehicleId, string origin, string destination, decimal weightKg, ShipmentStatus status,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, IEnumerable<StatusEntry> history) =>
        new(id, trackingNumber, parties, origin, destination, weightKg, status, createdAt, updatedAt,
            history.OrderBy(h => h.CreatedAt).ToList())
        {
            AssignedDriverId = driverId,
            AssignedDriverUserId = driverUserId,
            AssignedVehicleId = vehicleId
        };

    /// <summary>CREATED → DISPATCHED, snapshotting the driver and vehicle.</summary>
    public StatusEntry Dispatch(Guid driverId, Guid driverUserId, Guid vehicleId, string actorId)
    {
        Require(ShipmentStatus.CREATED, "dispatched");
        AssignedDriverId = driverId;
        AssignedDriverUserId = driverUserId;
        AssignedVehicleId = vehicleId;
        return Transition(ShipmentStatus.DISPATCHED, actorId, null);
    }

    /// <summary>DISPATCHED → IN_TRANSIT (package picked up).</summary>
    public StatusEntry Pickup(string actorId)
    {
        Require(ShipmentStatus.DISPATCHED, "picked up");
        return Transition(ShipmentStatus.IN_TRANSIT, actorId, null);
    }

    /// <summary>IN_TRANSIT → DELIVERED, with an optional proof-of-delivery note.</summary>
    public StatusEntry Deliver(string actorId, string? notes)
    {
        Require(ShipmentStatus.IN_TRANSIT, "delivered");
        return Transition(ShipmentStatus.DELIVERED, actorId, ShipmentRules.Notes(notes));
    }

    /// <summary>IN_TRANSIT → FAILED; a reason is mandatory.</summary>
    public StatusEntry Fail(string actorId, string? reason)
    {
        Require(ShipmentStatus.IN_TRANSIT, "marked as failed");
        var r = ShipmentRules.Notes(reason) ?? throw new DomainValidationException("A failure reason is required.");
        return Transition(ShipmentStatus.FAILED, actorId, r);
    }

    /// <summary>Any non-final state → CANCELLED. Senders may only cancel before dispatch.</summary>
    public StatusEntry Cancel(string actorId, string? reason, bool byAdmin)
    {
        if (IsFinal) throw new ConflictException($"A {Status} shipment cannot be cancelled.");
        if (!byAdmin && Status != ShipmentStatus.CREATED)
            throw new ConflictException("A shipment can only be cancelled by its sender before it is dispatched.");
        return Transition(ShipmentStatus.CANCELLED, actorId, ShipmentRules.Notes(reason));
    }

    private void Require(ShipmentStatus expected, string action)
    {
        if (Status != expected)
            throw new ConflictException($"A {Status} shipment cannot be {action} (expected {expected}).");
    }

    private StatusEntry Transition(ShipmentStatus to, string actorId, string? notes)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new StatusEntry(Guid.CreateVersion7(), to, notes, actorId, now);
        Status = to;
        UpdatedAt = now;
        _history.Add(entry);
        return entry;
    }
}
