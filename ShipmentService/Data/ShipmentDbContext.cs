using LastMile.Messaging;
using LastMile.Shipments.Models;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Shipments.Data;

public class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : DbContext(options)
{
    private const string Statuses = "'CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED', 'FAILED', 'CANCELLED'";

    public DbSet<ShipmentEntity> Shipments => Set<ShipmentEntity>();
    public DbSet<ShipmentStatusEntity> StatusHistory => Set<ShipmentStatusEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Kafka: outgoing shipment.lifecycle.v1 events (no consumer yet).
        b.AddOutbox();

        b.Entity<ShipmentEntity>(e =>
        {
            e.ToTable("shipments", t =>
            {
                t.HasCheckConstraint("ck_shipments_status", $"current_status IN ({Statuses})");
                t.HasCheckConstraint("ck_shipments_weight", "weight_kg > 0");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(20).IsRequired();
            // Logical references (no FKs across services) + user ids for ownership checks.
            e.Property(x => x.SenderId).HasColumnName("sender_id");
            e.Property(x => x.SenderUserId).HasColumnName("sender_user_id");
            e.Property(x => x.RecipientId).HasColumnName("recipient_id");
            e.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            e.Property(x => x.AssignedDriverId).HasColumnName("assigned_driver_id");
            e.Property(x => x.AssignedDriverUserId).HasColumnName("assigned_driver_user_id");
            e.Property(x => x.AssignedVehicleId).HasColumnName("assigned_vehicle_id");
            e.Property(x => x.OriginAddress).HasColumnName("origin_address").HasMaxLength(1000).IsRequired();
            e.Property(x => x.DestinationAddress).HasColumnName("destination_address").HasMaxLength(1000).IsRequired();
            e.Property(x => x.WeightKg).HasColumnName("weight_kg").HasPrecision(10, 2);
            e.Property(x => x.CurrentStatus).HasColumnName("current_status").HasMaxLength(20).IsRequired().HasDefaultValue("CREATED");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.TrackingNumber).IsUnique().HasDatabaseName("idx_shipments_tracking_number");
            e.HasIndex(x => x.SenderUserId).HasDatabaseName("idx_shipments_sender_user");
            e.HasIndex(x => x.RecipientUserId).HasDatabaseName("idx_shipments_recipient_user");
            e.HasIndex(x => x.AssignedDriverUserId).HasDatabaseName("idx_shipments_driver_user");
            // Named indexes: two different indexes on the same column.
            e.HasIndex(x => x.AssignedDriverId, "idx_shipments_assigned_driver");
            // D3: at most one active shipment per driver (closes the concurrent-dispatch race).
            e.HasIndex(x => x.AssignedDriverId, "ux_shipments_driver_active").IsUnique()
                .HasFilter("current_status IN ('DISPATCHED', 'IN_TRANSIT')");

            e.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(s => s.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ShipmentStatusEntity>(e =>
        {
            e.ToTable("shipment_status", t => t.HasCheckConstraint("ck_shipment_status_status", $"status IN ({Statuses})"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ShipmentId).HasColumnName("shipment_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            e.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
            e.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(255).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.ShipmentId, x.CreatedAt }).HasDatabaseName("idx_shipment_status_shipment_id");
        });
    }
}
