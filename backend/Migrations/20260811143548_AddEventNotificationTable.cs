using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventNotificationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "DateChange"),
                    OldDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_notifications_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_notifications_EventId",
                table: "event_notifications",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_notifications_Status_CreatedAt",
                table: "event_notifications",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_notifications");
        }
    }
}
