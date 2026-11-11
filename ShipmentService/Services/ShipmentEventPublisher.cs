using LastMile.Contracts.Shipment;
using LastMile.Messaging.Outbox;
using LastMile.Shipments.Core;
using LastMile.Shipments.Enums;

namespace LastMile.Shipments.Services;

/// <summary>
/// Stages <c>shipment.lifecycle.v1</c> events (key = shipmentId) in the outbox. Call it <b>before</b> the
/// <c>SaveChanges</c> that persists the transition, so status, history row and event commit together (NFR-4).
/// </summary>
public sealed class ShipmentEventPublisher(IEventOutbox outbox)
{
    public void Created(Shipment s) => outbox.Enqueue(s.Id.ToString(), new ShipmentCreated(s.Id, s.TrackingNumber,
        s.SenderId, s.RecipientId, s.OriginAddress, s.DestinationAddress, s.WeightKg));

    /// <summary>Publishes the event matching a transition's new status.</summary>
    public void Transitioned(Shipment s, StatusEntry entry)
    {
        var key = s.Id.ToString();
        switch (entry.Status)
        {
            case ShipmentStatus.DISPATCHED:
                outbox.Enqueue(key, new ShipmentDispatched(s.Id, s.TrackingNumber, s.AssignedDriverId!.Value, s.AssignedVehicleId!.Value));
                break;
            case ShipmentStatus.IN_TRANSIT:
                outbox.Enqueue(key, new ShipmentInTransit(s.Id, s.TrackingNumber, s.AssignedDriverId!.Value));
                break;
            case ShipmentStatus.DELIVERED:
                outbox.Enqueue(key, new ShipmentDelivered(s.Id, s.TrackingNumber, s.AssignedDriverId!.Value, entry.Notes));
                break;
            case ShipmentStatus.FAILED:
                outbox.Enqueue(key, new ShipmentFailed(s.Id, s.TrackingNumber, s.AssignedDriverId!.Value, entry.Notes!));
                break;
            case ShipmentStatus.CANCELLED:
                outbox.Enqueue(key, new ShipmentCancelled(s.Id, s.TrackingNumber, s.AssignedDriverId, entry.Notes));
                break;
        }
    }
}
