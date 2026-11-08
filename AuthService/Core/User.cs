using System.Net.Mail;
using LastMile.Auth.Enums;

namespace LastMile.Auth.Core;

/// <summary>
/// Domain model for a human user account. Encapsulates state and validation rules;
/// persistence concerns live in <see cref="Models.UserEntity"/>.
/// </summary>
public sealed class User
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxEmailLength = 255;

    public Guid Id { get; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private User(Guid id, string email, string passwordHash, UserRole role, bool isActive,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static User Create(string email, string passwordHash, UserRole role)
    {
        EnsureHumanRole(role);
        var now = DateTimeOffset.UtcNow;
        return new User(Guid.CreateVersion7(), NormalizeEmail(email), passwordHash, role, true, now, now);
    }

    /// <summary>Rebuilds a user from persisted state without re-running creation rules.</summary>
    public static User Rehydrate(Guid id, string email, string passwordHash, UserRole role, bool isActive,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, email, passwordHash, role, isActive, createdAt, updatedAt);

    public static string NormalizeEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length is 0 or > MaxEmailLength ||
            !MailAddress.TryCreate(normalized, out var parsed) || parsed.Address != normalized)
        {
            throw new DomainValidationException("A valid email address is required.");
        }
        return normalized;
    }

    public static void ValidatePasswordPolicy(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            throw new DomainValidationException(
                $"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new DomainValidationException("Password must contain at least one letter and one digit.");
    }

    public void EnsureCanAuthenticate()
    {
        if (!IsActive) throw new AuthenticationFailedException("This account is disabled.");
    }

    public void ChangeEmail(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized == Email) return;
        Email = normalized;
        Touch();
    }

    /// <summary>Replaces the password hash. The plain-text policy must be validated before hashing.</summary>
    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Touch();
    }

    public void ChangeRole(UserRole role)
    {
        EnsureHumanRole(role);
        if (role == Role) return;
        Role = role;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        if (isActive == IsActive) return;
        IsActive = isActive;
        Touch();
    }

    private static void EnsureHumanRole(UserRole role)
    {
        if (role == UserRole.M2M)
            throw new DomainValidationException("M2M is reserved for service clients and cannot be assigned to users.");
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
