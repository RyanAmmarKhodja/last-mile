using LastMile.Contracts.Profile;
using LastMile.Messaging.Outbox;
using LastMile.Profile.Core;
using LastMile.Profile.Enums;

namespace LastMile.Profile.Services;

/// <summary>
/// Stages <c>profile.client.v1</c> events in the outbox. Call it <b>before</b> the <c>SaveChanges</c> that persists the
/// change, so the event and the change commit atomically (documents/kafka-architecture.md §5).
/// </summary>
public sealed class ClientEventPublisher(IEventOutbox outbox)
{
    public void Created(ClientProfile p) => outbox.Enqueue(Key(p),
        new ClientCreated(p.Id, p.UserId, p.FirstName, p.LastName, p.CompanyName, p.PhoneNumber, p.Address));

    public void Updated(ClientProfile p) => outbox.Enqueue(Key(p),
        new ClientUpdated(p.Id, p.UserId, p.FirstName, p.LastName, p.CompanyName, p.PhoneNumber, p.Address));

    /// <summary>Publishes the event matching the profile's new status.</summary>
    public void StatusChanged(ClientProfile p)
    {
        switch (p.Status)
        {
            case ProfileStatus.ACTIVE: outbox.Enqueue(Key(p), new ClientReactivated(p.Id, p.UserId)); break;
            case ProfileStatus.INACTIVE: outbox.Enqueue(Key(p), new ClientDeactivated(p.Id, p.UserId)); break;
            case ProfileStatus.DELETED: outbox.Enqueue(Key(p), new ClientDeleted(p.Id, p.UserId)); break;
        }
    }

    private static string Key(ClientProfile p) => p.Id.ToString();
}
