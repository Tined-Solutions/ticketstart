using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketeraOnline.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReservationId",
                table: "Refunds",
                column: "ReservationId");

            // APR-014 legacy backfill: one Refunds row per pre-existing Refunded
            // Transaction (Status = 3, no HasConversion), AdminId NULL. Pure SQL via
            // migrationBuilder.Sql — ordered DDL that runs at APPLY time after the table
            // exists. NO EF-context backfill and NO exception swallowing: a DbContext
            // inside Up() would execute at SQL-generation time before the DDL applies
            // and silently never backfill (memory #442). gen_random_uuid() is native in
            // PG13+ (Supabase is PG15).
            migrationBuilder.Sql(@"
INSERT INTO ""Refunds"" (""Id"",""ReservationId"",""TicketIds"",""Quantity"",""Amount"",""AdminId"",""CreatedAt"")
SELECT gen_random_uuid(),
       t.""ReservationId"",
       COALESCE(agg.""TicketIds"", ARRAY[]::uuid[]),
       COALESCE(agg.""Quantity"", 0),
       t.""Amount"",
       NULL,
       t.""UpdatedAt""
FROM ""Transactions"" t
LEFT JOIN (
  SELECT ""ReservationId"", array_agg(""Id"") AS ""TicketIds"", COUNT(*) AS ""Quantity""
  FROM ""Tickets""
  WHERE ""IsRefunded"" = TRUE AND ""ReservationId"" IS NOT NULL
  GROUP BY ""ReservationId""
) agg ON agg.""ReservationId"" = t.""ReservationId""
WHERE t.""Status"" = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Reservations_ReservationId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_ReservationId",
                table: "Refunds");

            migrationBuilder.DropTable(
                name: "Refunds");
        }
    }
}
