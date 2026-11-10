using LastMile.Messaging;
using LastMile.Profile.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Profile.Data;

public class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    private const string StatusCheck = "status IN ('ACTIVE', 'INACTIVE', 'DELETED')";

    public DbSet<ClientEntity> Clients => Set<ClientEntity>();
    public DbSet<AdminEntity> Admins => Set<AdminEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Kafka: consumed auth.user.v1 event ids (idempotency) and outgoing profile.client.v1 events.
        b.AddInbox();
        b.AddOutbox();

        b.Entity<ClientEntity>(e =>
        {
            e.ToTable("clients", t => t.HasCheckConstraint("ck_clients_status", StatusCheck));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id"); // → AuthService.users (logical, no FK)
            e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(255);
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50).IsRequired();
            e.Property(x => x.Address).HasColumnName("address").HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("ACTIVE");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("idx_clients_user_id");
            e.HasIndex(x => new { x.Status, x.LastName, x.FirstName }).HasDatabaseName("idx_clients_status_name");
        });

        b.Entity<AdminEntity>(e =>
        {
            e.ToTable("admins", t => t.HasCheckConstraint("ck_admins_status", StatusCheck));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id"); // → AuthService.users (logical, no FK)
            e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("ACTIVE");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("idx_admins_user_id");
        });
    }
}
