using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using LastMile.Auth.Core;
using LastMile.Auth.Data;
using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using LastMile.Auth.Models;
using LastMile.Auth.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LastMile.Auth.Services;

/// <summary>
/// Authentication workflows: registration, credential verification, token issuance,
/// refresh-token rotation, logout/blacklisting and the OAuth 2.0 client credentials grant.
/// </summary>
public sealed class AuthService(
    AuthDbContext db,
    PasswordHashingService hasher,
    TokenService tokens,
    ITokenBlacklist blacklist,
    AuditService audit,
    IOptions<JwtOptions> options)
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "Invalid or expired refresh token.";

    public async Task<UserDto> RegisterAsync(RegisterDto dto, CancellationToken ct)
    {
        var role = dto.Role!.Value;
        if (role is not (UserRole.CLIENT or UserRole.DRIVER))
            throw new DomainValidationException("Self-registration is only available for CLIENT and DRIVER accounts.");

        var email = User.NormalizeEmail(dto.Email);
        User.ValidatePasswordPolicy(dto.Password);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = User.Create(email, hasher.Hash(dto.Password), role);
        db.Users.Add(user.ToEntity());
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.UserRegistered, true, user.Id.ToString(), user.Email, $"role={user.Role}", ct);
        return UserDto.From(user);
    }

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var entity = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (entity is null)
        {
            hasher.SimulateVerify(dto.Password);
            await audit.LogAsync(AuditAction.LoginFailed, false, null, email, "unknown email", ct);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        var user = entity.ToDomain();
        if (!hasher.Verify(user.PasswordHash, dto.Password))
        {
            await audit.LogAsync(AuditAction.LoginFailed, false, user.Id.ToString(), email, "bad password", ct);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (!user.IsActive)
        {
            await audit.LogAsync(AuditAction.LoginFailed, false, user.Id.ToString(), email, "account disabled", ct);
            user.EnsureCanAuthenticate();
        }

        var response = await IssueTokenPairAsync(user, rotatedFrom: null, ct);
        await audit.LogAsync(AuditAction.LoginSucceeded, true, user.Id.ToString(), email, ct: ct);
        return response;
    }

    public async Task<TokenResponseDto> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.HashToken(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            await audit.LogAsync(AuditAction.TokenRefreshFailed, false, null, null, "unknown refresh token", ct);
            throw new AuthenticationFailedException(InvalidRefreshToken);
        }

        var userId = stored.UserId.ToString();
        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedByTokenHash is not null)
            {
                // A rotated token was presented again: it has likely been stolen. Kill the whole session family.
                var revoked = await db.RevokeAllRefreshTokensAsync(stored.UserId, "reuse detected", ct);
                await audit.LogAsync(AuditAction.RefreshTokenReuseDetected, false, userId, stored.User.Email,
                    $"revoked {revoked} active refresh token(s)", ct);
            }
            else
            {
                await audit.LogAsync(AuditAction.TokenRefreshFailed, false, userId, stored.User.Email, "revoked token", ct);
            }
            throw new AuthenticationFailedException(InvalidRefreshToken);
        }

        if (stored.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await audit.LogAsync(AuditAction.TokenRefreshFailed, false, userId, stored.User.Email, "expired token", ct);
            throw new AuthenticationFailedException(InvalidRefreshToken);
        }

        var user = stored.User.ToDomain();
        if (!user.IsActive)
        {
            await audit.LogAsync(AuditAction.TokenRefreshFailed, false, userId, user.Email, "account disabled", ct);
            user.EnsureCanAuthenticate();
        }

        var response = await IssueTokenPairAsync(user, rotatedFrom: stored, ct);
        await audit.LogAsync(AuditAction.TokenRefreshed, true, userId, user.Email, ct: ct);
        return response;
    }

    public async Task LogoutAsync(ClaimsPrincipal principal, string? refreshToken, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var jti = principal.GetJti();
        var expiresAt = principal.GetExpiry();
        if (jti is not null && expiresAt is not null)
            await blacklist.RevokeAsync(jti, userId.ToString(), expiresAt.Value, "logout", ct);

        var refreshRevoked = false;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = TokenService.HashToken(refreshToken);
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(
                t => t.TokenHash == hash && t.UserId == userId && t.RevokedAt == null, ct);
            if (stored is not null)
            {
                stored.RevokedAt = DateTimeOffset.UtcNow;
                stored.RevokedReason = "logout";
                await db.SaveChangesAsync(ct);
                refreshRevoked = true;
            }
        }

        await audit.LogAsync(AuditAction.Logout, true, userId.ToString(), userId.ToString(),
            $"access_jti={jti} refresh_revoked={refreshRevoked}", ct);
    }

    /// <summary>OAuth 2.0 client credentials grant (RFC 6749 §4.4).</summary>
    public async Task<M2MTokenResponseDto> IssueM2MTokenAsync(M2MTokenRequestDto request, string? authorizationHeader,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.GrantType))
            throw OAuthException.InvalidRequest("The 'grant_type' parameter is required.");
        if (request.GrantType != TokenClaims.ClientCredentialsGrant)
            throw OAuthException.UnsupportedGrantType();

        var (clientId, clientSecret) = ReadClientCredentials(request, authorizationHeader);
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw OAuthException.InvalidClient();

        var entity = await db.ServiceClients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
        if (entity is null)
            hasher.SimulateVerify(clientSecret);
        if (entity is null || !entity.IsActive || !hasher.Verify(entity.SecretHash, clientSecret))
        {
            await audit.LogAsync(AuditAction.M2MTokenDenied, false, clientId, null,
                entity is null ? "unknown client" : !entity.IsActive ? "client disabled" : "bad secret", ct);
            throw OAuthException.InvalidClient();
        }

        var client = entity.ToDomain();
        IReadOnlyList<string> scopes;
        string audience;
        try
        {
            scopes = client.ResolveScopes(request.Scope);
            audience = client.ResolveAudience(request.Audience);
        }
        catch (OAuthException ex)
        {
            await audit.LogAsync(AuditAction.M2MTokenDenied, false, clientId, request.Audience, ex.Message, ct);
            throw;
        }

        var token = tokens.CreateM2MToken(client, audience, scopes);
        var scope = string.Join(' ', scopes);
        await audit.LogAsync(AuditAction.M2MTokenIssued, true, clientId, audience, $"jti={token.Jti} scope={scope}", ct);
        return new M2MTokenResponseDto(token.Token, "Bearer", token.ExpiresIn, scope);
    }

    private async Task<TokenResponseDto> IssueTokenPairAsync(User user, RefreshTokenEntity? rotatedFrom,
        CancellationToken ct)
    {
        var access = tokens.CreateUserAccessToken(user);
        var refreshToken = TokenService.GenerateRefreshToken();
        var refreshHash = TokenService.HashToken(refreshToken);
        var now = DateTimeOffset.UtcNow;
        var refreshExpiresAt = now.AddDays(options.Value.RefreshTokenDays);

        if (rotatedFrom is not null)
        {
            rotatedFrom.RevokedAt = now;
            rotatedFrom.RevokedReason = "rotated";
            rotatedFrom.ReplacedByTokenHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = audit.ClientIp
        });
        await db.SaveChangesAsync(ct);

        return new TokenResponseDto(access.Token, refreshToken, "Bearer", access.ExpiresIn, refreshExpiresAt);
    }

    /// <summary>Client credentials from HTTP Basic auth (preferred by RFC 6749 §2.3.1) or the form body.</summary>
    private static (string? ClientId, string? ClientSecret) ReadClientCredentials(M2MTokenRequestDto request,
        string? authorizationHeader)
    {
        if (AuthenticationHeaderValue.TryParse(authorizationHeader, out var header) &&
            header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) && header.Parameter is not null)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
                var separator = decoded.IndexOf(':');
                if (separator > 0)
                    return (Uri.UnescapeDataString(decoded[..separator]), Uri.UnescapeDataString(decoded[(separator + 1)..]));
            }
            catch (FormatException)
            {
                throw OAuthException.InvalidClient();
            }
        }
        return (request.ClientId?.Trim(), request.ClientSecret);
    }
}
