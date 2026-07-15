using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentlyReserved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentlyReserved",
                table: "TicketTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentlyReserved",
                table: "TicketTypes");
        }
    }
}
