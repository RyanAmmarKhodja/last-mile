using LastMile.Profile.Core;
using LastMile.Profile.Enums;
using LastMile.Profile.Models;

namespace LastMile.Profile.Data;

/// <summary>Translates between domain models (Core) and persistence models (Models).</summary>
public static class Mappers
{
    public static ClientProfile ToDomain(this ClientEntity e) =>
        ClientProfile.Rehydrate(e.Id, e.UserId, e.FirstName, e.LastName, e.CompanyName, e.PhoneNumber, e.Address,
            Enum.Parse<ProfileStatus>(e.Status), e.CreatedAt, e.UpdatedAt);

    public static ClientEntity ToEntity(this ClientProfile p)
    {
        var entity = new ClientEntity();
        p.ApplyTo(entity);
        return entity;
    }

    public static void ApplyTo(this ClientProfile p, ClientEntity e)
    {
        e.Id = p.Id;
        e.UserId = p.UserId;
        e.FirstName = p.FirstName;
        e.LastName = p.LastName;
        e.CompanyName = p.CompanyName;
        e.PhoneNumber = p.PhoneNumber;
        e.Address = p.Address;
        e.Status = p.Status.ToString();
        e.CreatedAt = p.CreatedAt;
        e.UpdatedAt = p.UpdatedAt;
    }

    public static AdminProfile ToDomain(this AdminEntity e) =>
        AdminProfile.Rehydrate(e.Id, e.UserId, e.FirstName, e.LastName, e.PhoneNumber,
            Enum.Parse<ProfileStatus>(e.Status), e.CreatedAt, e.UpdatedAt);

    public static AdminEntity ToEntity(this AdminProfile p)
    {
        var entity = new AdminEntity();
        p.ApplyTo(entity);
        return entity;
    }

    public static void ApplyTo(this AdminProfile p, AdminEntity e)
    {
        e.Id = p.Id;
        e.UserId = p.UserId;
        e.FirstName = p.FirstName;
        e.LastName = p.LastName;
        e.PhoneNumber = p.PhoneNumber;
        e.Status = p.Status.ToString();
        e.CreatedAt = p.CreatedAt;
        e.UpdatedAt = p.UpdatedAt;
    }
}
