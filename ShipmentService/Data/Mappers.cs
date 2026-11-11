using LastMile.Shipments.Core;
using LastMile.Shipments.Enums;
using LastMile.Shipments.Models;

namespace LastMile.Shipments.Data;

/// <summary>Translates between domain models (Core) and persistence models (Models).</summary>
public static class Mappers
{
    /// <summary>Requires <see cref="ShipmentEntity.StatusHistory"/> to be loaded.</summary>
    public static Shipment ToDomain(this ShipmentEntity e) =>
        Shipment.Rehydrate(e.Id, e.TrackingNumber,
            new ShipmentParties(e.SenderId, e.SenderUserId, e.RecipientId, e.RecipientUserId),
            e.AssignedDriverId, e.AssignedDriverUserId, e.AssignedVehicleId, e.OriginAddress, e.DestinationAddress,
            e.WeightKg, Enum.Parse<ShipmentStatus>(e.CurrentStatus), e.CreatedAt, e.UpdatedAt,
            e.StatusHistory.Select(ToDomain));

    public static StatusEntry ToDomain(this ShipmentStatusEntity e) =>
        new(e.Id, Enum.Parse<ShipmentStatus>(e.Status), e.Notes, e.ActorId, e.CreatedAt);

    /// <summary>New shipment with its initial history.</summary>
    public static ShipmentEntity ToEntity(this Shipment s)
    {
        var entity = new ShipmentEntity
        {
            Id = s.Id,
            TrackingNumber = s.TrackingNumber,
            SenderId = s.SenderId,
            SenderUserId = s.SenderUserId,
            RecipientId = s.RecipientId,
            RecipientUserId = s.RecipientUserId,
            OriginAddress = s.OriginAddress,
            DestinationAddress = s.DestinationAddress,
            WeightKg = s.WeightKg,
            CreatedAt = s.CreatedAt
        };
        s.ApplyTo(entity);
        entity.StatusHistory.AddRange(s.History.Select(h => h.ToEntity(s.Id)));
        return entity;
    }

    /// <summary>Copies the mutable state (status, assignment). New history rows are added with <see cref="ToEntity(StatusEntry, Guid)"/>.</summary>
    public static void ApplyTo(this Shipment s, ShipmentEntity e)
    {
        e.AssignedDriverId = s.AssignedDriverId;
        e.AssignedDriverUserId = s.AssignedDriverUserId;
        e.AssignedVehicleId = s.AssignedVehicleId;
        e.CurrentStatus = s.Status.ToString();
        e.UpdatedAt = s.UpdatedAt;
    }

    public static ShipmentStatusEntity ToEntity(this StatusEntry h, Guid shipmentId) => new()
    {
        Id = h.Id,
        ShipmentId = shipmentId,
        Status = h.Status.ToString(),
        Notes = h.Notes,
        ActorId = h.ActorId,
        CreatedAt = h.CreatedAt
    };
}
