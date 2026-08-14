using Microsoft.EntityFrameworkCore.Migrations;
using TicketeraOnline.Api.Data;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// EA-001/EA-006: adds the NOT NULL Event.Status column (int, default 0 =
    /// Pending) and then runs the best-effort backfill that flips ALL pre-existing
    /// events (expired included) to Approved so they keep their public visibility.
    /// </summary>
    public partial class AddEventApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // EA-006 backfill: best-effort. The schema change is the critical part;
            // if the design-time factory cannot resolve or the save throws, log and
            // continue — existing events would stay Pending(0), hidden until an admin
            // approves them (accepted fallback, migration never aborts).
            try
            {
                using var context = new ApplicationDbContextFactory().CreateDbContext(null);
                EventApprovalBackfill.RunAsync(context).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // NOTE: positional {0} — a named placeholder with WriteLine(format, arg)
                // throws FormatException inside the catch and aborts the migration.
                Console.Error.WriteLine(
                    "[AddEventApproval] Backfill skipped for {0}; existing events keep Status=Pending(0).",
                    ex.Message);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Events");
        }
    }
}
