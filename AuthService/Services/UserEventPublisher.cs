using LastMile.Auth.Core;
using LastMile.Auth.Enums;
using LastMile.Contracts.Auth;
using LastMile.Messaging.Outbox;
using ContractRole = LastMile.Contracts.Auth.UserRole;

namespace LastMile.Auth.Services;

/// <summary>
/// Stages <c>auth.user.v1</c> events in the outbox. Call it <b>before</b> the <c>SaveChanges</c> that persists the
/// change, so the event and the change commit atomically (documents/kafka-architecture.md §5).
/// </summary>
public sealed class UserEventPublisher(IEventOutbox outbox)
{
    public void Registered(User user, RegistrationProfile profile) =>
        outbox.Enqueue(Key(user), new UserRegistered(user.Id, user.Email, Map(user.Role), profile));

    public void EmailChanged(User user) =>
        outbox.Enqueue(Key(user), new UserEmailChanged(user.Id, user.Email));

    public void RoleChanged(User user, Enums.UserRole previousRole) =>
        outbox.Enqueue(Key(user), new UserRoleChanged(user.Id, Map(previousRole), Map(user.Role)));

    public void ActiveChanged(User user)
    {
        if (user.IsActive) outbox.Enqueue(Key(user), new UserReactivated(user.Id, Map(user.Role)));
        else outbox.Enqueue(Key(user), new UserDeactivated(user.Id, Map(user.Role)));
    }

    public void Deleted(Guid userId, Enums.UserRole role) =>
        outbox.Enqueue(userId.ToString(), new UserDeleted(userId, Map(role)));

    private static string Key(User user) => user.Id.ToString();

    private static ContractRole Map(Enums.UserRole role) => role switch
    {
        Enums.UserRole.ADMIN => ContractRole.ADMIN,
        Enums.UserRole.DRIVER => ContractRole.DRIVER,
        Enums.UserRole.CLIENT => ContractRole.CLIENT,
        _ => throw new InvalidOperationException($"Role {role} is not a user role.")
    };
}
