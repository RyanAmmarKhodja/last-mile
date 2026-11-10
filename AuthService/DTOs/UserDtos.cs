using System.ComponentModel.DataAnnotations;
using LastMile.Auth.Core;
using LastMile.Auth.Enums;

namespace LastMile.Auth.DTOs;

public sealed record UserDto(
    Guid Id,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt);
}

/// <summary>Admin-only account creation; any human role including ADMIN.</summary>
public sealed class CreateUserDto
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public UserRole? Role { get; init; }

    /// <summary>Profile fields relayed in <c>UserRegistered</c> (not stored by AuthService).</summary>
    [Required]
    public RegistrationProfileDto? Profile { get; init; }
}

/// <summary>Partial update; omitted fields are left unchanged. Role and IsActive are admin-only.</summary>
public sealed class UpdateUserDto
{
    [EmailAddress, MaxLength(255)]
    public string? Email { get; init; }

    [MinLength(8), MaxLength(128)]
    public string? Password { get; init; }

    /// <summary>Required when a non-admin changes their own email or password.</summary>
    public string? CurrentPassword { get; init; }

    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
