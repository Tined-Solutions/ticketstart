using Microsoft.EntityFrameworkCore;

namespace TicketeraOnline.Api.Data;

/// <summary>
/// Application database context for Ticketera Online.
/// Configured to use Supabase PostgreSQL with connection pooling.
/// Runtime: Port 6543 (Transaction mode pooler)
/// Migrations: Port 5432 (Direct connection)
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets will be added as entities are created
    // public DbSet<User> Users { get; set; }
    // public DbSet<Event> Events { get; set; }
    // public DbSet<TicketType> TicketTypes { get; set; }
    // public DbSet<Reservation> Reservations { get; set; }
    // public DbSet<Ticket> Tickets { get; set; }
    // public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations will be added here as entities are created
    }
}
