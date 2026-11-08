using Microsoft.AspNetCore.Identity;

namespace LastMile.Auth.Services;

/// <summary>
/// PBKDF2 (HMAC-SHA512, 100k iterations, per-hash salt) via ASP.NET Core Identity's PasswordHasher.
/// Used for user passwords and service client secrets.
/// </summary>
public sealed class PasswordHashingService
{
    private static readonly object Owner = new();
    private readonly PasswordHasher<object> _hasher = new();
    private readonly Lazy<string> _dummyHash;

    public PasswordHashingService()
    {
        _dummyHash = new Lazy<string>(() => Hash(Guid.NewGuid().ToString()));
    }

    public string Hash(string secret) => _hasher.HashPassword(Owner, secret);

    public bool Verify(string hash, string secret) =>
        _hasher.VerifyHashedPassword(Owner, hash, secret) != PasswordVerificationResult.Failed;

    /// <summary>
    /// Burns the same CPU time as a real verification so response timing does not reveal
    /// whether an account exists.
    /// </summary>
    public void SimulateVerify(string secret) => Verify(_dummyHash.Value, secret);
}
