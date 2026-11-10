using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;

namespace LastMile.Auth.Options;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>"iss" claim of every token issued by this service.</summary>
    public string Issuer { get; set; } = "https://auth.lastmile.internal";

    /// <summary>"aud" claim of user access tokens (the API gateway / platform APIs).</summary>
    public string Audience { get; set; } = "last-mile-api";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public int M2MTokenMinutes { get; set; } = 60;
}

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Bootstrap administrator, created only if no ADMIN user exists yet.</summary>
    public SeedAdmin? Admin { get; set; }

    /// <summary>
    /// Demo accounts (any role) created on startup if their email does not exist yet. Each one publishes
    /// <c>UserRegistered</c> with its profile, so downstream services get a matching profile. Development only.
    /// </summary>
    public List<SeedUser> Users { get; set; } = [];

    /// <summary>Service clients created on startup if their client_id does not exist yet.</summary>
    public List<SeedServiceClient> ServiceClients { get; set; } = [];

    public sealed class SeedAdmin
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = "System";
        public string LastName { get; set; } = "Administrator";
    }

    public sealed class SeedUser
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.CLIENT;
        public RegistrationProfileDto Profile { get; set; } = new();
    }

    public sealed class SeedServiceClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string ClientSecret { get; set; } = string.Empty;
        public List<string> Scopes { get; set; } = [];
        public List<string> Audiences { get; set; } = [];
    }
}
