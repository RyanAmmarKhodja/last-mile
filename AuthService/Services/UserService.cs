using System.Security.Claims;
using LastMile.Auth.Core;
using LastMile.Auth.Data;
using LastMile.Auth.DTOs;
using LastMile.Auth.Enums;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Auth.Services;

/// <summary>User account management (CRUD) with admin / owner access rules.</summary>
public sealed class UserService(AuthDbContext db, PasswordHashingService hasher, AuditService audit)
{
    public const int MaxPageSize = 100;

    public async Task<UserDto> GetAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct)
    {
        EnsureAdminOrOwner(actor, id);
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                     ?? throw new NotFoundException("User not found.");
        return UserDto.From(entity.ToDomain());
    }

    public async Task<PagedResultDto<UserDto>> ListAsync(int page, int pageSize, UserRole? role, string? search,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Users.AsNoTracking();
        if (role is not null)
        {
            var roleName = role.Value.ToString();
            query = query.Where(u => u.Role == roleName);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PagedResultDto<UserDto>(items.Select(e => UserDto.From(e.ToDomain())).ToList(), page, pageSize, total);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var email = User.NormalizeEmail(dto.Email);
        User.ValidatePasswordPolicy(dto.Password);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = User.Create(email, hasher.Hash(dto.Password), dto.Role!.Value);
        db.Users.Add(user.ToEntity());
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.UserCreated, true, actor.GetUserId().ToString(), user.Id.ToString(),
            $"email={user.Email} role={user.Role}", ct);
        return UserDto.From(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorId = actor.GetUserId();
        var isAdmin = actor.IsAdmin();
        EnsureAdminOrOwner(actor, id);

        if (!isAdmin && (dto.Role is not null || dto.IsActive is not null))
            throw new ForbiddenException("Only administrators can change a role or account status.");

        var entity = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                     ?? throw new NotFoundException("User not found.");
        var user = entity.ToDomain();
        var changes = new List<string>();

        var changingCredentials = dto.Email is not null || dto.Password is not null;
        if (changingCredentials && !isAdmin &&
            (string.IsNullOrEmpty(dto.CurrentPassword) || !hasher.Verify(user.PasswordHash, dto.CurrentPassword)))
        {
            await audit.LogAsync(AuditAction.UserUpdated, false, actorId.ToString(), id.ToString(),
                "current password missing or incorrect", ct);
            throw new DomainValidationException("The current password is missing or incorrect.");
        }

        if (dto.Email is not null)
        {
            var email = User.NormalizeEmail(dto.Email);
            if (email != user.Email)
            {
                if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct))
                    throw new ConflictException("An account with this email already exists.");
                user.ChangeEmail(email);
                changes.Add("email");
            }
        }

        if (dto.Password is not null)
        {
            User.ValidatePasswordPolicy(dto.Password);
            user.ChangePasswordHash(hasher.Hash(dto.Password));
            changes.Add("password");
        }

        var losesAdmin = user.Role == UserRole.ADMIN && user.IsActive &&
                         ((dto.Role is not null && dto.Role != UserRole.ADMIN) || dto.IsActive == false);
        if (losesAdmin) await EnsureAnotherActiveAdminAsync(id, ct);

        if (dto.Role is not null && dto.Role != user.Role)
        {
            user.ChangeRole(dto.Role.Value);
            changes.Add($"role={user.Role}");
        }

        if (dto.IsActive is not null && dto.IsActive != user.IsActive)
        {
            user.SetActive(dto.IsActive.Value);
            changes.Add($"is_active={user.IsActive}");
        }

        if (changes.Count == 0) return UserDto.From(user);

        user.ApplyTo(entity);
        await db.SaveChangesAsync(ct);

        // Credentials, role or status changed: force every session to re-authenticate.
        if (changes.Any(c => c is "password" || c.StartsWith("role") || c.StartsWith("is_active")))
            await db.RevokeAllRefreshTokensAsync(id, "account updated", ct);

        await audit.LogAsync(AuditAction.UserUpdated, true, actorId.ToString(), id.ToString(),
            string.Join(' ', changes), ct);
        return UserDto.From(user);
    }

    public async Task DeleteAsync(Guid id, ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorId = actor.GetUserId();
        EnsureAdminOrOwner(actor, id);

        var entity = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                     ?? throw new NotFoundException("User not found.");
        if (entity.Role == Roles.Admin && entity.IsActive)
            await EnsureAnotherActiveAdminAsync(id, ct);

        db.Users.Remove(entity); // refresh tokens cascade
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.UserDeleted, true, actorId.ToString(), id.ToString(),
            $"email={entity.Email} role={entity.Role}", ct);
    }

    private static void EnsureAdminOrOwner(ClaimsPrincipal actor, Guid targetId)
    {
        if (!actor.IsAdmin() && actor.GetUserId() != targetId)
            throw new ForbiddenException("You can only access your own account.");
    }

    private async Task EnsureAnotherActiveAdminAsync(Guid excludingId, CancellationToken ct)
    {
        var others = await db.Users.CountAsync(u => u.Role == Roles.Admin && u.IsActive && u.Id != excludingId, ct);
        if (others == 0)
            throw new ConflictException("This is the last active administrator and cannot be removed or demoted.");
    }
}
