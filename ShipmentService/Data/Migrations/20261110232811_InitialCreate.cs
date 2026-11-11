using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LastMile.Shipments.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tracking_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_driver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    destination_address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    current_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "CREATED"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.id);
                    table.CheckConstraint("ck_shipments_status", "current_status IN ('CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED', 'FAILED', 'CANCELLED')");
                    table.CheckConstraint("ck_shipments_weight", "weight_kg > 0");
                });

            migrationBuilder.CreateTable(
                name: "shipment_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    actor_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_status", x => x.id);
                    table.CheckConstraint("ck_shipment_status_status", "status IN ('CREATED', 'DISPATCHED', 'IN_TRANSIT', 'DELIVERED', 'FAILED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_shipment_status_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_outbox_event_id",
                table: "outbox",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_outbox_published_at",
                table: "outbox",
                column: "published_at",
                filter: "published_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_unpublished",
                table: "outbox",
                column: "id",
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_shipment_status_shipment_id",
                table: "shipment_status",
                columns: new[] { "shipment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_shipments_assigned_driver",
                table: "shipments",
                column: "assigned_driver_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_driver_user",
                table: "shipments",
                column: "assigned_driver_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_recipient_user",
                table: "shipments",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_sender_user",
                table: "shipments",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_tracking_number",
                table: "shipments",
                column: "tracking_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_shipments_driver_active",
                table: "shipments",
                column: "assigned_driver_id",
                unique: true,
                filter: "current_status IN ('DISPATCHED', 'IN_TRANSIT')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox");

            migrationBuilder.DropTable(
                name: "shipment_status");

            migrationBuilder.DropTable(
                name: "shipments");
        }
    }
}
