using System.Security.Claims;
using LastMile.Auth.Core;
using LastMile.Auth.Data;
using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Auth.Services;

/// <summary>Management of M2M service accounts (OAuth 2.0 confidential clients).</summary>
public sealed class ServiceClientService(AuthDbContext db, PasswordHashingService hasher, AuditService audit)
{
    public async Task<IReadOnlyList<ServiceClientDto>> ListAsync(CancellationToken ct) =>
        (await db.ServiceClients.AsNoTracking().OrderBy(c => c.ClientId).ToListAsync(ct))
        .Select(e => ToDto(e.ToDomain())).ToList();

    public async Task<ServiceClientCredentialsDto> CreateAsync(CreateServiceClientDto dto, ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var secret = TokenService.GenerateClientSecret();
        var client = await CreateInternalAsync(dto.ClientId, dto.Name, secret, dto.Scopes, dto.Audiences, ct);
        await audit.LogAsync(AuditAction.ServiceClientCreated, true, actor.GetUserId().ToString(), client.ClientId,
            $"scopes={string.Join(' ', client.AllowedScopes)} audiences={string.Join(' ', client.AllowedAudiences)}", ct);
        return new ServiceClientCredentialsDto(ToDto(client), secret);
    }

    /// <summary>Creates a client with a known secret (startup seeding). No-op when the client_id exists.</summary>
    public async Task<bool> EnsureExistsAsync(string clientId, string? name, string secret, IEnumerable<string> scopes,
        IEnumerable<string> audiences, CancellationToken ct)
    {
        if (await db.ServiceClients.AnyAsync(c => c.ClientId == clientId, ct)) return false;
        var client = await CreateInternalAsync(clientId, name, secret, scopes, audiences, ct);
        await audit.LogAsync(AuditAction.ServiceClientCreated, true, "system", client.ClientId, "seeded", ct);
        return true;
    }

    public async Task<ServiceClientCredentialsDto> RotateSecretAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await db.ServiceClients.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException("Service client not found.");
        var secret = TokenService.GenerateClientSecret();
        entity.SecretHash = hasher.Hash(secret);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.ServiceClientSecretRotated, true, actor.GetUserId().ToString(), entity.ClientId, ct: ct);
        return new ServiceClientCredentialsDto(ToDto(entity.ToDomain()), secret);
    }

    public async Task DeleteAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct)
    {
        var entity = await db.ServiceClients.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException("Service client not found.");
        db.ServiceClients.Remove(entity);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(AuditAction.ServiceClientDeleted, true, actor.GetUserId().ToString(), entity.ClientId, ct: ct);
    }

    private async Task<ServiceClient> CreateInternalAsync(string clientId, string? name, string secret,
        IEnumerable<string> scopes, IEnumerable<string> audiences, CancellationToken ct)
    {
        var client = ServiceClient.Create(clientId, name ?? clientId, hasher.Hash(secret), scopes, audiences);
        if (await db.ServiceClients.AnyAsync(c => c.ClientId == client.ClientId, ct))
            throw new ConflictException($"A service client with client_id '{client.ClientId}' already exists.");
        db.ServiceClients.Add(client.ToEntity());
        await db.SaveChangesAsync(ct);
        return client;
    }

    private static ServiceClientDto ToDto(ServiceClient c) =>
        new(c.Id, c.ClientId, c.Name, c.AllowedScopes, c.AllowedAudiences, c.IsActive, c.CreatedAt);
}
