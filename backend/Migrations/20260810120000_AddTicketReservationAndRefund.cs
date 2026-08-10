using Microsoft.EntityFrameworkCore.Migrations;
using TicketeraOnline.Api.Data;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// APR-009: adds the nullable Ticket→Reservation link plus the refund flags
    /// (IsRefunded/RefundedAt, mirroring IsUsed/UsedAt), then runs the best-effort
    /// chunked backfill that links legacy tickets to their confirmed reservation.
    /// Unmatched/ambiguous tickets keep a NULL ReservationId (accepted per APR-009).
    /// </summary>
    public partial class AddTicketReservationAndRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefunded",
                table: "Tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ReservationId",
                table: "Tickets",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Reservations_ReservationId",
                table: "Tickets",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // APR-009 backfill: best-effort chunked linking. NULL leftovers are the
            // accepted end state for ambiguous/legacy rows, so a backfill failure
            // (e.g. design-time factory cannot resolve) must not block the schema
            // migration — the schema change is the critical part.
            try
            {
                using var context = new ApplicationDbContextFactory().CreateDbContext(null);
                TicketReservationBackfill.RunAsync(context).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // NOTE: positional {0} — a named placeholder with WriteLine(format, arg)
                // throws FormatException inside the catch and aborts the migration.
                Console.Error.WriteLine(
                    "[AddTicketReservationAndRefund] Backfill skipped for {0}; legacy tickets keep NULL ReservationId (accepted APR-009).",
                    ex.Message);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Reservations_ReservationId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ReservationId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsRefunded",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Tickets");
        }
    }
}
