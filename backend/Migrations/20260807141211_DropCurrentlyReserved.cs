using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropCurrentlyReserved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentlyReserved",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TicketTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentlyReserved",
                table: "TicketTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TicketTypes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "gen_random_bytes(8)");
        }
    }
}
