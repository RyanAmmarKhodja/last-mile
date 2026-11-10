using System.Security.Claims;
using LastMile.Profile.Core;
using LastMile.Profile.Data;
using LastMile.Profile.DTOs;
using LastMile.Profile.Enums;
using LastMile.Profile.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Profile.Services;

/// <summary>Client profile use cases with owner / admin / M2M access rules.</summary>
public sealed class ClientProfileService(ProfileDbContext db, ClientEventPublisher events, ILogger<ClientProfileService> logger)
{
    public async Task<ClientProfileDto> GetMineAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (entity is null || entity.Status == nameof(ProfileStatus.DELETED))
            throw new NotFoundException("No client profile exists for this account yet. It is created shortly after " +
                                        "registration; you can also create it with PUT /api/v1/profiles/clients/me.");
        return ClientProfileDto.From(entity.ToDomain());
    }

    /// <summary>Creates the caller's profile if missing, otherwise replaces its details.</summary>
    public async Task<(ClientProfileDto Profile, bool Created)> UpsertMineAsync(ClaimsPrincipal actor,
        UpdateClientProfileDto dto, CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (entity is null)
        {
            var created = ClientProfile.Create(userId, dto.ToDetails());
            db.Clients.Add(created.ToEntity());
            events.Created(created); // same transaction as the row
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Client profile {ProfileId} created by owner {UserId}", created.Id, userId);
            return (ClientProfileDto.From(created), true);
        }

        return (await UpdateAsync(entity, dto, actor, ct), false);
    }

    public async Task<PagedResultDto<ClientProfileDto>> ListAsync(int page, int pageSize, string? search,
        ProfileStatus? status, CancellationToken ct)
    {
        (page, pageSize) = ProfileQueries.Normalize(page, pageSize);
        var statusName = ProfileQueries.StatusOrDefault(status);
        var query = db.Clients.AsNoTracking().Where(c => c.Status == statusName);

        if (ProfileQueries.SearchPattern(search) is { } pattern)
            query = query.Where(c => EF.Functions.ILike(c.FirstName, pattern) || EF.Functions.ILike(c.LastName, pattern) ||
                                     (c.CompanyName != null && EF.Functions.ILike(c.CompanyName, pattern)) ||
                                     EF.Functions.ILike(c.PhoneNumber, pattern));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return ProfileQueries.Page(items.Select(e => ClientProfileDto.From(e.ToDomain())).ToList(), page, pageSize, total);
    }

    public async Task<ClientProfileDto> GetAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct) =>
        Authorize(await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct), actor);

    public async Task<ClientProfileDto> GetByUserAsync(Guid userId, ClaimsPrincipal actor, CancellationToken ct) =>
        Authorize(await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct), actor);

    /// <summary>Administrator edit of any client profile.</summary>
    public async Task<ClientProfileDto> UpdateAsync(Guid id, UpdateClientProfileDto dto, ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var entity = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException("Client profile not found.");
        return await UpdateAsync(entity, dto, actor, ct);
    }

    private async Task<ClientProfileDto> UpdateAsync(ClientEntity entity, UpdateClientProfileDto dto, ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var profile = entity.ToDomain();
        if (!profile.Update(dto.ToDetails())) return ClientProfileDto.From(profile);

        profile.ApplyTo(entity);
        events.Updated(profile); // same transaction as the change
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Client profile {ProfileId} updated by {Actor}", profile.Id, actor.ActorId());
        return ClientProfileDto.From(profile);
    }

    /// <summary>
    /// ADMIN and services with profiles:read see every profile (including DELETED, for shipment history).
    /// A CLIENT sees only their own, non-deleted profile.
    /// </summary>
    private static ClientProfileDto Authorize(ClientEntity? entity, ClaimsPrincipal actor)
    {
        if (actor.CanReadAllClients())
            return entity is null ? throw new NotFoundException("Client profile not found.") : ClientProfileDto.From(entity.ToDomain());

        var userId = actor.GetUserId();
        if (entity is not null && entity.UserId != userId)
            throw new ForbiddenException("You can only access your own profile.");
        if (entity is null || entity.Status == nameof(ProfileStatus.DELETED))
            throw new NotFoundException("Client profile not found.");
        return ClientProfileDto.From(entity.ToDomain());
    }
}
