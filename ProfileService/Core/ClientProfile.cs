using LastMile.Profile.Enums;

namespace LastMile.Profile.Core;

/// <summary>Editable details of a client profile (validated by <see cref="ClientProfile"/>).</summary>
public sealed record ClientDetails(string? FirstName, string? LastName, string? CompanyName, string? PhoneNumber, string? Address);

/// <summary>
/// Domain model of a client (shipment sender or recipient). Encapsulates validation and status transitions;
/// persistence lives in <see cref="Models.ClientEntity"/>.
/// </summary>
public sealed class ClientProfile
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? CompanyName { get; private set; }
    public string PhoneNumber { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public ProfileStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ClientProfile(Guid id, Guid userId, ProfileStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static ClientProfile Create(Guid userId, ClientDetails details)
    {
        if (userId == Guid.Empty) throw new DomainValidationException("A user id is required.");
        var now = DateTimeOffset.UtcNow;
        var profile = new ClientProfile(Guid.CreateVersion7(), userId, ProfileStatus.ACTIVE, now, now);
        profile.Apply(details);
        return profile;
    }

    public static ClientProfile Rehydrate(Guid id, Guid userId, string firstName, string lastName, string? companyName,
        string phoneNumber, string address, ProfileStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, userId, status, createdAt, updatedAt)
        {
            FirstName = firstName,
            LastName = lastName,
            CompanyName = companyName,
            PhoneNumber = phoneNumber,
            Address = address
        };

    /// <summary>Replaces all editable fields. Returns false when nothing changed.</summary>
    public bool Update(ClientDetails details)
    {
        EnsureNotDeleted();
        var before = (FirstName, LastName, CompanyName, PhoneNumber, Address);
        Apply(details);
        if (before == (FirstName, LastName, CompanyName, PhoneNumber, Address)) return false;
        Touch();
        return true;
    }

    public bool Deactivate() => ChangeStatus(ProfileStatus.ACTIVE, ProfileStatus.INACTIVE);
    public bool Reactivate() => ChangeStatus(ProfileStatus.INACTIVE, ProfileStatus.ACTIVE);

    /// <summary>Final state: the account was deleted. Returns false when already deleted.</summary>
    public bool MarkDeleted()
    {
        if (Status == ProfileStatus.DELETED) return false;
        Status = ProfileStatus.DELETED;
        Touch();
        return true;
    }

    public void EnsureNotDeleted()
    {
        if (Status == ProfileStatus.DELETED)
            throw new ConflictException("This profile belongs to a deleted account and cannot be changed.");
    }

    private void Apply(ClientDetails d)
    {
        FirstName = ProfileRules.Required(d.FirstName, "First name", ProfileRules.MaxNameLength);
        LastName = ProfileRules.Required(d.LastName, "Last name", ProfileRules.MaxNameLength);
        CompanyName = ProfileRules.Optional(d.CompanyName, "Company name", ProfileRules.MaxCompanyLength);
        PhoneNumber = ProfileRules.Required(d.PhoneNumber, "Phone number", ProfileRules.MaxPhoneLength);
        Address = ProfileRules.Required(d.Address, "Address", ProfileRules.MaxAddressLength);
    }

    private bool ChangeStatus(ProfileStatus from, ProfileStatus to)
    {
        if (Status != from) return false;
        Status = to;
        Touch();
        return true;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
