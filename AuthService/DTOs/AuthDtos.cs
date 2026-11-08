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
