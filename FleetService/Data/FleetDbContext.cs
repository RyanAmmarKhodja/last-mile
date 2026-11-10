using LastMile.Fleet.Models;
using LastMile.Messaging;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Fleet.Data;

public class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options)
{
    public DbSet<DriverEntity> Drivers => Set<DriverEntity>();
    public DbSet<VehicleEntity> Vehicles => Set<VehicleEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Kafka: consumed auth.user.v1 event ids (idempotency) and outgoing fleet.driver.v1 / fleet.vehicle.v1 events.
        b.AddInbox();
        b.AddOutbox();

        b.Entity<DriverEntity>(e =>
        {
            e.ToTable("drivers", t =>
            {
                t.HasCheckConstraint("ck_drivers_status", "status IN ('ACTIVE', 'INACTIVE', 'DELETED')");
                // D3: only an ACTIVE driver can be available.
                t.HasCheckConstraint("ck_drivers_available_active", "NOT is_available OR status = 'ACTIVE'");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id"); // → AuthService.users (logical, no FK)
            e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.LicenseNumber).HasColumnName("license_number").HasMaxLength(100).IsRequired();
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50).IsRequired();
            e.Property(x => x.IsAvailable).HasColumnName("is_available").HasDefaultValue(false);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("ACTIVE");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("idx_drivers_user_id");
            e.HasIndex(x => x.LicenseNumber).IsUnique().HasDatabaseName("idx_drivers_license_number");
            e.HasIndex(x => new { x.Status, x.IsAvailable }).HasDatabaseName("idx_drivers_status_available");

            // One vehicle per driver: vehicles.assigned_driver_id is unique and nulls out if a driver row is ever removed.
            e.HasOne(x => x.Vehicle).WithOne().HasForeignKey<VehicleEntity>(v => v.AssignedDriverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<VehicleEntity>(e =>
        {
            e.ToTable("vehicles", t =>
            {
                t.HasCheckConstraint("ck_vehicles_capacity", "capacity_kg > 0");
                // A retired vehicle has no driver.
                t.HasCheckConstraint("ck_vehicles_assigned_active", "assigned_driver_id IS NULL OR is_active");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.LicensePlate).HasColumnName("license_plate").HasMaxLength(50).IsRequired();
            e.Property(x => x.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
            e.Property(x => x.CapacityKg).HasColumnName("capacity_kg").HasPrecision(10, 2);
            e.Property(x => x.AssignedDriverId).HasColumnName("assigned_driver_id");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.LicensePlate).IsUnique().HasDatabaseName("idx_vehicles_license_plate");
            e.HasIndex(x => x.AssignedDriverId).IsUnique().HasDatabaseName("idx_vehicles_assigned_driver_id");
        });
    }
}
