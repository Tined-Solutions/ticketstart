using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationPurchaserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_MercadoPagoId",
                table: "Transactions");

            migrationBuilder.AddColumn<string>(
                name: "PurchaserEmail",
                table: "Reservations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MercadoPagoId",
                table: "Transactions",
                column: "MercadoPagoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_MercadoPagoId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PurchaserEmail",
                table: "Reservations");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MercadoPagoId",
                table: "Transactions",
                column: "MercadoPagoId");
        }
    }
}
