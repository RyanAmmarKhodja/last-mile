using System.ComponentModel.DataAnnotations;
using LastMile.Auth.Enums;

namespace LastMile.Auth.DTOs;

public sealed class RegisterDto
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    /// <summary>CLIENT or DRIVER. ADMIN accounts can only be created by an administrator.</summary>
    [Required]
    public UserRole? Role { get; init; }

    /// <summary>Profile fields relayed to ProfileService / FleetService in <c>UserRegistered</c> (not stored here).</summary>
    [Required]
    public RegistrationProfileDto? Profile { get; init; }
}

/// <summary>
/// Profile collected at registration (decision D2, documents/data-ownership-matrix.md §8.1).
/// Per-role required fields are checked by <see cref="Core.RegistrationProfileRules"/>.
/// </summary>
public sealed class RegistrationProfileDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    /// <summary>Required for CLIENT and DRIVER.</summary>
    [MaxLength(50)]
    public string? PhoneNumber { get; init; }

    /// <summary>CLIENT only, optional.</summary>
    [MaxLength(255)]
    public string? CompanyName { get; init; }

    /// <summary>CLIENT only, required.</summary>
    [MaxLength(1000)]
    public string? Address { get; init; }

    /// <summary>DRIVER only, required.</summary>
    [MaxLength(100)]
    public string? LicenseNumber { get; init; }
}

public sealed class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class LogoutDto
{
    /// <summary>Refresh token to revoke. The access token used to call /logout is always blacklisted.</summary>
    public string? RefreshToken { get; init; }
}

public sealed record TokenResponseDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Identity resolved from a valid token, returned by /validate (used by NGINX auth_request).</summary>
public sealed record TokenIntrospectionDto(
    string Subject,
    string? Role,
    string? Email,
    string? ClientId,
    string? Scope,
    DateTimeOffset? ExpiresAt);
