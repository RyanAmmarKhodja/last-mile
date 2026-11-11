using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMile.Tracking.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GPS audit trail (FleetTrackingService.md §5): PostGIS, partitioned by day on recorded_at, kept 30 days.
            // EF cannot model partitioned tables, so the entity is ExcludeFromMigrations() and the table is created here.
            // Daily partitions are created (and dropped) by HistoryMaintenance; the default partition is a safety net.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");
            migrationBuilder.Sql("""
                CREATE TABLE location_history (
                    driver_id   UUID NOT NULL,
                    vehicle_id  UUID,
                    shipment_id UUID,
                    latitude    DOUBLE PRECISION NOT NULL,
                    longitude   DOUBLE PRECISION NOT NULL,
                    position    GEOGRAPHY(Point, 4326)
                        GENERATED ALWAYS AS (ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography) STORED,
                    speed_kmh   REAL,
                    heading_deg REAL,
                    accuracy_m  REAL,
                    recorded_at TIMESTAMPTZ NOT NULL,
                    received_at TIMESTAMPTZ NOT NULL,
                    PRIMARY KEY (driver_id, recorded_at)
                ) PARTITION BY RANGE (recorded_at);
                CREATE INDEX idx_location_history_shipment ON location_history (shipment_id, recorded_at)
                    WHERE shipment_id IS NOT NULL;
                CREATE TABLE location_history_default PARTITION OF location_history DEFAULT;
                """);

            migrationBuilder.CreateTable(
                name: "inbox",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "tracked_drivers",
                columns: table => new
                {
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracked_drivers", x => x.driver_id);
                    table.CheckConstraint("ck_tracked_drivers_status", "status IN ('ACTIVE', 'INACTIVE', 'DELETED')");
                });

            migrationBuilder.CreateTable(
                name: "tracked_shipments",
                columns: table => new
                {
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracked_shipments", x => x.shipment_id);
                    table.CheckConstraint("ck_tracked_shipments_status", "status IN ('CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED', 'FAILED', 'CANCELLED')");
                });

            migrationBuilder.CreateIndex(
                name: "idx_inbox_processed_at",
                table: "inbox",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "idx_tracked_drivers_user_id",
                table: "tracked_drivers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tracked_shipments_driver_active",
                table: "tracked_shipments",
                column: "driver_id",
                filter: "status IN ('DISPATCHED', 'IN_TRANSIT')");

            migrationBuilder.CreateIndex(
                name: "idx_tracked_shipments_tracking_number",
                table: "tracked_shipments",
                column: "tracking_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS location_history CASCADE;");

            migrationBuilder.DropTable(
                name: "inbox");

            migrationBuilder.DropTable(
                name: "tracked_drivers");

            migrationBuilder.DropTable(
                name: "tracked_shipments");
        }
    }
}
