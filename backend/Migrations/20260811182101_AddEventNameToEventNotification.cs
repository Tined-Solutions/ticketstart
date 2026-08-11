using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventNameToEventNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventName",
                table: "event_notifications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventName",
                table: "event_notifications");
        }
    }
}
