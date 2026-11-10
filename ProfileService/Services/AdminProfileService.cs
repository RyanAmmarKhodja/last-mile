using System.Security.Claims;
using LastMile.Profile.Core;
using LastMile.Profile.Data;
using LastMile.Profile.DTOs;
using LastMile.Profile.Enums;
using LastMile.Profile.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Profile.Services;

/// <summary>Administrator profile use cases (all endpoints are ADMIN-only).</summary>
public sealed class AdminProfileService(ProfileDbContext db, ILogger<AdminProfileService> logger)
{
    public async Task<AdminProfileDto> GetMineAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await db.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (entity is null || entity.Status == nameof(ProfileStatus.DELETED))
            throw new NotFoundException("No administrator profile exists for this account yet. Create it with " +
                                        "PUT /api/v1/profiles/admins/me.");
        return AdminProfileDto.From(entity.ToDomain());
    }

    public async Task<(AdminProfileDto Profile, bool Created)> UpsertMineAsync(ClaimsPrincipal actor,
        UpdateAdminProfileDto dto, CancellationToken ct)
    {
        var userId = actor.GetUserId();
        var entity = await db.Admins.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (entity is null)
        {
            var created = AdminProfile.Create(userId, dto.ToDetails());
            db.Admins.Add(created.ToEntity());
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Admin profile {ProfileId} created by owner {UserId}", created.Id, userId);
            return (AdminProfileDto.From(created), true);
        }
        return (await UpdateAsync(entity, dto, actor, ct), false);
    }

    public async Task<PagedResultDto<AdminProfileDto>> ListAsync(int page, int pageSize, string? search,
        ProfileStatus? status, CancellationToken ct)
    {
        (page, pageSize) = ProfileQueries.Normalize(page, pageSize);
        var statusName = ProfileQueries.StatusOrDefault(status);
        var query = db.Admins.AsNoTracking().Where(a => a.Status == statusName);

        if (ProfileQueries.SearchPattern(search) is { } pattern)
            query = query.Where(a => EF.Functions.ILike(a.FirstName, pattern) || EF.Functions.ILike(a.LastName, pattern) ||
                                     (a.PhoneNumber != null && EF.Functions.ILike(a.PhoneNumber, pattern)));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return ProfileQueries.Page(items.Select(e => AdminProfileDto.From(e.ToDomain())).ToList(), page, pageSize, total);
    }

    public async Task<AdminProfileDto> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
                     ?? throw new NotFoundException("Administrator profile not found.");
        return AdminProfileDto.From(entity.ToDomain());
    }

    public async Task<AdminProfileDto> UpdateAsync(Guid id, UpdateAdminProfileDto dto, ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var entity = await db.Admins.FirstOrDefaultAsync(a => a.Id == id, ct)
                     ?? throw new NotFoundException("Administrator profile not found.");
        return await UpdateAsync(entity, dto, actor, ct);
    }

    private async Task<AdminProfileDto> UpdateAsync(AdminEntity entity, UpdateAdminProfileDto dto, ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var profile = entity.ToDomain();
        if (!profile.Update(dto.ToDetails())) return AdminProfileDto.From(profile);
        profile.ApplyTo(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin profile {ProfileId} updated by {Actor}", profile.Id, actor.ActorId());
        return AdminProfileDto.From(profile);
    }
}
