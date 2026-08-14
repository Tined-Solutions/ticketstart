using System.IO;
using System.Linq;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED structural test for the AddRefundsTable migration (APR-014).
/// Asserts that `{ts}_AddRefunds.cs` + `{ts}_AddRefunds.Designer.cs` exist and that
/// Up() runs the legacy backfill as PURE SQL (INSERT…SELECT with array_agg) — never
/// via an EF DbContext and never inside try/catch.
///
/// Why pure SQL matters (memory #442): a DbContext created inside Up() executes at
/// SQL-generation time, BEFORE the DDL applies, fails on first run and is swallowed
/// by the surrounding catch — the backfill silently never happens. migrationBuilder.Sql
/// emits ordered DDL that runs at APPLY time after the table exists.
/// </summary>
public class AddRefundsTable_BackfillContainsPureSqlInsertSelect
{
    // The migration's Up() holds the SQL as a C# verbatim string, so double-quotes
    // are escaped as "" in the file content.
    private const string BackfillInsert =
        "INSERT INTO \"\"Refunds\"\" (\"\"Id\"\",\"\"ReservationId\"\",\"\"TicketIds\"\",\"\"Quantity\"\",\"\"Amount\"\",\"\"AdminId\"\",\"\"CreatedAt\"\")";

    private static string[] MigrationSourceFiles()
    {
        // Tests run from bin/Debug/net9.0 — walk up until the repo's Migrations
        // folder (containing *_AddRefunds.cs) is found.
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var migrationsDir = Path.Combine(dir.FullName, "Migrations");
            if (Directory.Exists(migrationsDir) &&
                Directory.GetFiles(migrationsDir, "*_AddRefunds*.cs").Length > 0)
            {
                return Directory.GetFiles(migrationsDir, "*_AddRefunds*.cs");
            }
            dir = dir.Parent;
        }
        return Array.Empty<string>();
    }

    [Fact]
    public void Migration_And_Designer_Files_Exist()
    {
        // The Designer file is MANDATORY: without it EF silently does not discover
        // the migration (memory #442).
        var migration = MigrationSourceFiles().SingleOrDefault(f => !f.EndsWith(".Designer.cs"));
        var designer = MigrationSourceFiles().SingleOrDefault(f => f.EndsWith(".Designer.cs"));

        Assert.NotNull(migration);
        Assert.NotNull(designer);
    }

    [Fact]
    public void Up_Contains_Pure_Sql_InsertSelect_Backfill()
    {
        var migration = MigrationSourceFiles().Single(f => !f.EndsWith(".Designer.cs"));
        var source = File.ReadAllText(migration);

        // Backfill must be a migrationBuilder.Sql INSERT…SELECT (runs at apply time).
        Assert.Contains("migrationBuilder.Sql", source);
        Assert.Contains(BackfillInsert, source);
        Assert.Contains("SELECT gen_random_uuid()", source);
        Assert.Contains("array_agg", source);
        Assert.Contains("WHERE t.\"\"Status\"\" = 3", source); // Refunded = 3 (no HasConversion)

        // NO EF-context backfill, NO try/catch — fail loudly, never swallow.
        Assert.DoesNotContain("ApplicationDbContext", source);
        Assert.DoesNotContain("try", source);
        Assert.DoesNotContain("catch", source);
    }
}
