namespace LastMile.Auth.Models;

/// <summary>Persistence model mapped to the "users" table.</summary>
public class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<RefreshTokenEntity> RefreshTokens { get; set; } = [];
}
