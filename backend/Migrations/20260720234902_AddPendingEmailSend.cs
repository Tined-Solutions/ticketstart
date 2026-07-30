using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEmailSend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_email_send",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TicketIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_email_send", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_email_send_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pending_email_send_ReservationId",
                table: "pending_email_send",
                column: "ReservationId");

            // Composite index on (status, created_at) for efficient retry queries
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_pending_email_send_status_created_at
                    ON pending_email_send (status, created_at ASC);
            ");

            // RLS policy: enable row-level security, allow service_role full access
            migrationBuilder.Sql(@"
                ALTER TABLE pending_email_send ENABLE ROW LEVEL SECURITY;
            ");

            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS pending_email_send_service_role_policy ON pending_email_send;
            ");

            migrationBuilder.Sql(@"
                CREATE POLICY pending_email_send_service_role_policy ON pending_email_send
                    FOR ALL
                    TO service_role
                    USING (true)
                    WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS pending_email_send_service_role_policy ON pending_email_send;
            ");

            migrationBuilder.DropTable(
                name: "pending_email_send");
        }
    }
}
