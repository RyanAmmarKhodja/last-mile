using LastMile.Contracts.Auth;
using LastMile.Messaging.Inbox;
using LastMile.Profile.Core;
using LastMile.Profile.Data;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Profile.Services;

/// <summary>
/// Applies <c>auth.user.v1</c> events to profiles (documents/ProfileService/ProfileService.md §8).
/// Runs inside the consumer's transaction: changes, outgoing events and the inbox row commit together.
/// Every branch is idempotent, so a redelivered event changes nothing.
/// </summary>
public sealed class UserEventHandler(ProfileDbContext db, ClientEventPublisher clientEvents, ILogger<UserEventHandler> logger)
{
    public Task HandleAsync(ConsumedEvent evt, CancellationToken ct)
    {
        if (evt.Is<UserRegistered>()) return OnRegisteredAsync(evt.Data<UserRegistered>(), ct);
        if (evt.Is<UserDeactivated>()) return SetStatusAsync(evt.Data<UserDeactivated>().UserId, null, Transition.Deactivate, ct);
        if (evt.Is<UserReactivated>())
        {
            var e = evt.Data<UserReactivated>();
            return SetStatusAsync(e.UserId, e.Role, Transition.Reactivate, ct);
        }
        if (evt.Is<UserRoleChanged>()) return OnRoleChangedAsync(evt.Data<UserRoleChanged>(), ct);
        if (evt.Is<UserDeleted>()) return SetStatusAsync(evt.Data<UserDeleted>().UserId, null, Transition.Delete, ct);
        return Task.CompletedTask; // UserEmailChanged and future types: not relevant here
    }

    private async Task OnRegisteredAsync(UserRegistered e, CancellationToken ct)
    {
        try
        {
            switch (e.Role)
            {
                case UserRole.CLIENT when !await db.Clients.AnyAsync(c => c.UserId == e.UserId, ct):
                    var client = ClientProfile.Create(e.UserId, new ClientDetails(e.Profile.FirstName, e.Profile.LastName,
                        e.Profile.CompanyName, e.Profile.PhoneNumber, e.Profile.Address));
                    db.Clients.Add(client.ToEntity());
                    clientEvents.Created(client);
                    logger.LogInformation("Client profile {ProfileId} created for user {UserId}", client.Id, e.UserId);
                    break;

                case UserRole.ADMIN when !await db.Admins.AnyAsync(a => a.UserId == e.UserId, ct):
                    var admin = AdminProfile.Create(e.UserId,
                        new AdminDetails(e.Profile.FirstName, e.Profile.LastName, e.Profile.PhoneNumber));
                    db.Admins.Add(admin.ToEntity());
                    logger.LogInformation("Admin profile {ProfileId} created for user {UserId}", admin.Id, e.UserId);
                    break;
                // DRIVER: FleetService. Existing profile: redelivery or created earlier by PUT /me, nothing to do.
            }
        }
        catch (DomainValidationException ex)
        {
            // AuthService validates the same rules, so this means a broken producer: retrying cannot help.
            throw new PermanentEventException($"UserRegistered for {e.UserId} has an invalid profile: {ex.Message}", ex);
        }
    }

    private async Task OnRoleChangedAsync(UserRoleChanged e, CancellationToken ct)
    {
        await SetStatusAsync(e.UserId, e.PreviousRole, Transition.Deactivate, ct);
        await SetStatusAsync(e.UserId, e.Role, Transition.Reactivate, ct);
    }

    private enum Transition { Deactivate, Reactivate, Delete }

    /// <summary>Applies a status transition to the user's client and/or admin profile (<paramref name="role"/> null = both).</summary>
    private async Task SetStatusAsync(Guid userId, UserRole? role, Transition transition, CancellationToken ct)
    {
        if (role is null or UserRole.CLIENT &&
            await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId, ct) is { } clientEntity)
        {
            var client = clientEntity.ToDomain();
            if (Apply(transition, client.Deactivate, client.Reactivate, client.MarkDeleted))
            {
                client.ApplyTo(clientEntity);
                clientEvents.StatusChanged(client);
                logger.LogInformation("Client profile {ProfileId} is now {Status}", client.Id, client.Status);
            }
        }

        if (role is null or UserRole.ADMIN &&
            await db.Admins.FirstOrDefaultAsync(a => a.UserId == userId, ct) is { } adminEntity)
        {
            var admin = adminEntity.ToDomain();
            if (Apply(transition, admin.Deactivate, admin.Reactivate, admin.MarkDeleted))
            {
                admin.ApplyTo(adminEntity);
                logger.LogInformation("Admin profile {ProfileId} is now {Status}", admin.Id, admin.Status);
            }
        }
    }

    private static bool Apply(Transition t, Func<bool> deactivate, Func<bool> reactivate, Func<bool> delete) => t switch
    {
        Transition.Deactivate => deactivate(),
        Transition.Reactivate => reactivate(),
        _ => delete()
    };
}
