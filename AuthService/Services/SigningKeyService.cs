using System.Security.Cryptography;
using LastMile.Auth.Data;
using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using LastMile.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LastMile.Auth.Services;

/// <summary>
/// Holds the RSA keys used to sign JWTs (RS256). Keys are persisted in the signing_keys table so they
/// survive restarts and are shared by all replicas; the newest active key signs, all active keys verify
/// and are published through JWKS (which allows rotating keys without invalidating live tokens).
/// </summary>
public sealed class SigningKeyService(IServiceScopeFactory scopeFactory, ILogger<SigningKeyService> logger)
{
    public const string Algorithm = SecurityAlgorithms.RsaSha256;

    private KeyRing? _ring;

    private KeyRing Ring => _ring ?? throw new InvalidOperationException("Signing keys have not been initialized.");

    public SigningCredentials SigningCredentials => new(Ring.Current, Algorithm);
    public IReadOnlyList<SecurityKey> ValidationKeys => Ring.All;
    public JwksDto Jwks => Ring.Jwks;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var rows = await db.SigningKeys.AsNoTracking()
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            using var rsa = RSA.Create(2048);
            var row = new SigningKeyEntity
            {
                KeyId = Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..32],
                PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
                Algorithm = "RS256",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.SigningKeys.Add(row);
            await db.SaveChangesAsync(ct);
            await scope.ServiceProvider.GetRequiredService<AuditService>()
                .LogAsync(AuditAction.SigningKeyGenerated, true, "system", row.KeyId, ct: ct);
            rows = [row];
        }

        var keys = rows.Select(r =>
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(r.PrivateKeyPem);
            return new RsaSecurityKey(rsa) { KeyId = r.KeyId };
        }).ToList();

        var jwks = new JwksDto(keys.Select(k =>
        {
            var p = k.Rsa.ExportParameters(includePrivateParameters: false);
            return new JwkDto("RSA", "sig", "RS256", k.KeyId, Base64UrlEncoder.Encode(p.Modulus), Base64UrlEncoder.Encode(p.Exponent));
        }).ToList());

        _ring = new KeyRing(keys[0], keys, jwks);
        logger.LogInformation("Loaded {Count} signing key(s); active kid={Kid}", keys.Count, keys[0].KeyId);
    }

    private sealed record KeyRing(RsaSecurityKey Current, IReadOnlyList<SecurityKey> All, JwksDto Jwks);
}
