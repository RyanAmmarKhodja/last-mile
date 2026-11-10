using LastMile.Auth.Models;
using LastMile.Messaging;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Auth.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<RevokedTokenEntity> RevokedTokens => Set<RevokedTokenEntity>();
    public DbSet<ServiceClientEntity> ServiceClients => Set<ServiceClientEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<SigningKeyEntity> SigningKeys => Set<SigningKeyEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Transactional outbox for auth.user.v1 events (documents/kafka-architecture.md §5).
        b.AddOutbox();

        b.Entity<UserEntity>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint("ck_users_role", "role IN ('ADMIN', 'DRIVER', 'CLIENT')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("idx_users_email");
        });

        b.Entity<RefreshTokenEntity>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(64);
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.RevokedReason).HasColumnName("revoked_reason").HasMaxLength(100);
            e.Property(x => x.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("idx_refresh_tokens_token_hash");
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_refresh_tokens_user_id");
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RevokedTokenEntity>(e =>
        {
            e.ToTable("revoked_tokens");
            e.HasKey(x => x.Jti);
            e.Property(x => x.Jti).HasColumnName("jti").HasMaxLength(64);
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(100);
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_revoked_tokens_expires_at");
        });

        b.Entity<ServiceClientEntity>(e =>
        {
            e.ToTable("service_clients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ClientId).HasColumnName("client_id").HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(x => x.SecretHash).HasColumnName("secret_hash").HasMaxLength(255).IsRequired();
            e.Property(x => x.AllowedScopes).HasColumnName("allowed_scopes");
            e.Property(x => x.AllowedAudiences).HasColumnName("allowed_audiences");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.ClientId).IsUnique().HasDatabaseName("idx_service_clients_client_id");
        });

        b.Entity<AuditLogEntity>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
            e.Property(x => x.Success).HasColumnName("success");
            e.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(255);
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(255);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            e.Property(x => x.Details).HasColumnName("details");
            e.HasIndex(x => x.OccurredAt).HasDatabaseName("idx_audit_logs_occurred_at");
            e.HasIndex(x => x.ActorId).HasDatabaseName("idx_audit_logs_actor_id");
        });

        b.Entity<SigningKeyEntity>(e =>
        {
            e.ToTable("signing_keys");
            e.HasKey(x => x.KeyId);
            e.Property(x => x.KeyId).HasColumnName("kid").HasMaxLength(64);
            e.Property(x => x.PrivateKeyPem).HasColumnName("private_key_pem").IsRequired();
            e.Property(x => x.Algorithm).HasColumnName("algorithm").HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        });
    }
}
