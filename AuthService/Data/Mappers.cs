using LastMile.Auth.Core;
using LastMile.Auth.Enums;
using LastMile.Auth.Models;

namespace LastMile.Auth.Data;

/// <summary>Translates between domain models (Core) and persistence models (Models).</summary>
public static class Mappers
{
    public static User ToDomain(this UserEntity e) =>
        User.Rehydrate(e.Id, e.Email, e.PasswordHash, Enum.Parse<UserRole>(e.Role), e.IsActive, e.CreatedAt, e.UpdatedAt);

    public static UserEntity ToEntity(this User u)
    {
        var entity = new UserEntity();
        u.ApplyTo(entity);
        return entity;
    }

    public static void ApplyTo(this User u, UserEntity e)
    {
        e.Id = u.Id;
        e.Email = u.Email;
        e.PasswordHash = u.PasswordHash;
        e.Role = u.Role.ToString();
        e.IsActive = u.IsActive;
        e.CreatedAt = u.CreatedAt;
        e.UpdatedAt = u.UpdatedAt;
    }

    public static ServiceClient ToDomain(this ServiceClientEntity e) =>
        ServiceClient.Rehydrate(e.Id, e.ClientId, e.Name, e.SecretHash, e.AllowedScopes, e.AllowedAudiences,
            e.IsActive, e.CreatedAt);

    public static ServiceClientEntity ToEntity(this ServiceClient c) => new()
    {
        Id = c.Id,
        ClientId = c.ClientId,
        Name = c.Name,
        SecretHash = c.SecretHash,
        AllowedScopes = [.. c.AllowedScopes],
        AllowedAudiences = [.. c.AllowedAudiences],
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt
    };
}
