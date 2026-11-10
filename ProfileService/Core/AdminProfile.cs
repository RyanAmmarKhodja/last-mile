using LastMile.Profile.Enums;

namespace LastMile.Profile.Core;

/// <summary>Editable details of an administrator profile (validated by <see cref="AdminProfile"/>).</summary>
public sealed record AdminDetails(string? FirstName, string? LastName, string? PhoneNumber);

/// <summary>Domain model of an administrator profile; persistence lives in <see cref="Models.AdminEntity"/>.</summary>
public sealed class AdminProfile
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public ProfileStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private AdminProfile(Guid id, Guid userId, ProfileStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static AdminProfile Create(Guid userId, AdminDetails details)
    {
        if (userId == Guid.Empty) throw new DomainValidationException("A user id is required.");
        var now = DateTimeOffset.UtcNow;
        var profile = new AdminProfile(Guid.CreateVersion7(), userId, ProfileStatus.ACTIVE, now, now);
        profile.Apply(details);
        return profile;
    }

    public static AdminProfile Rehydrate(Guid id, Guid userId, string firstName, string lastName, string? phoneNumber,
        ProfileStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, userId, status, createdAt, updatedAt)
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber
        };

    /// <summary>Replaces all editable fields. Returns false when nothing changed.</summary>
    public bool Update(AdminDetails details)
    {
        EnsureNotDeleted();
        var before = (FirstName, LastName, PhoneNumber);
        Apply(details);
        if (before == (FirstName, LastName, PhoneNumber)) return false;
        Touch();
        return true;
    }

    public bool Deactivate() => ChangeStatus(ProfileStatus.ACTIVE, ProfileStatus.INACTIVE);
    public bool Reactivate() => ChangeStatus(ProfileStatus.INACTIVE, ProfileStatus.ACTIVE);

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

    private void Apply(AdminDetails d)
    {
        FirstName = ProfileRules.Required(d.FirstName, "First name", ProfileRules.MaxNameLength);
        LastName = ProfileRules.Required(d.LastName, "Last name", ProfileRules.MaxNameLength);
        PhoneNumber = ProfileRules.Optional(d.PhoneNumber, "Phone number", ProfileRules.MaxPhoneLength);
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
