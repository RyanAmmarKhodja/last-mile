using LastMile.Messaging;
using LastMile.Tracking.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Tracking.Data;

public class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<TrackedDriverEntity> Drivers => Set<TrackedDriverEntity>();
    public DbSet<TrackedShipmentEntity> Shipments => Set<TrackedShipmentEntity>();
    public DbSet<LocationHistoryEntity> History => Set<LocationHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Kafka: consumed fleet.driver.v1 / shipment.lifecycle.v1 event ids (idempotency). No outbox: publishes nothing.
        b.AddInbox();

        b.Entity<TrackedDriverEntity>(e =>
        {
            e.ToTable("tracked_drivers", t =>
                t.HasCheckConstraint("ck_tracked_drivers_status", "status IN ('ACTIVE', 'INACTIVE', 'DELETED')"));
            e.HasKey(x => x.DriverId);
            e.Property(x => x.DriverId).HasColumnName("driver_id").ValueGeneratedNever(); // → FleetService.drivers
            e.Property(x => x.UserId).HasColumnName("user_id");                         // → AuthService.users
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            e.Property(x => x.IsAvailable).HasColumnName("is_available").HasDefaultValue(false);
            e.Property(x => x.VehicleId).HasColumnName("vehicle_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("idx_tracked_drivers_user_id");
        });

        b.Entity<TrackedShipmentEntity>(e =>
        {
            e.ToTable("tracked_shipments", t => t.HasCheckConstraint("ck_tracked_shipments_status",
                "status IN ('CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED', 'FAILED', 'CANCELLED')"));
            e.HasKey(x => x.ShipmentId);
            e.Property(x => x.ShipmentId).HasColumnName("shipment_id").ValueGeneratedNever(); // → ShipmentService.shipments
            e.Property(x => x.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            e.Property(x => x.DriverId).HasColumnName("driver_id");
            e.Property(x => x.VehicleId).HasColumnName("vehicle_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.TrackingNumber).IsUnique().HasDatabaseName("idx_tracked_shipments_tracking_number");
            e.HasIndex(x => x.DriverId).HasDatabaseName("idx_tracked_shipments_driver_active")
                .HasFilter("status IN ('DISPATCHED', 'IN_TRANSIT')");
        });

        // Partitioned by day: created with raw SQL in the migration, so EF must not try to create it.
        b.Entity<LocationHistoryEntity>(e =>
        {
            e.ToTable("location_history", t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.DriverId, x.RecordedAt });
            e.Property(x => x.DriverId).HasColumnName("driver_id");
            e.Property(x => x.VehicleId).HasColumnName("vehicle_id");
            e.Property(x => x.ShipmentId).HasColumnName("shipment_id");
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.SpeedKmh).HasColumnName("speed_kmh");
            e.Property(x => x.HeadingDeg).HasColumnName("heading_deg");
            e.Property(x => x.AccuracyM).HasColumnName("accuracy_m");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
        });
    }
}
