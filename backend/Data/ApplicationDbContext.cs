using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Models;

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

    public DbSet<User> Users { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<TicketType> TicketTypes { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.Property(u => u.Name).HasMaxLength(200).IsRequired(false);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
        });

        // Event entity configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrganizerId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Location).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Organizer)
                .WithMany(u => u.OrganizedEvents)
                .HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TicketType entity configuration
        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.EventId);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Price).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(t => t.Quantity).IsRequired();
            entity.Property(t => t.CurrentlyReserved).IsRequired().HasDefaultValue(0);
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.Property(t => t.RowVersion).IsRowVersion();

            entity.HasOne(t => t.Event)
                .WithMany(e => e.TicketTypes)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Reservation entity configuration
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.EventId);
            entity.HasIndex(r => r.TicketTypeId);
            entity.HasIndex(r => r.UserId);
            entity.Property(r => r.Quantity).IsRequired();
            entity.Property(r => r.PurchaserDNI).IsRequired().HasMaxLength(50);
            entity.Property(r => r.ExpiresAt).IsRequired();
            entity.Property(r => r.Status).IsRequired();
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(r => r.Event)
                .WithMany()
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.TicketType)
                .WithMany()
                .HasForeignKey(r => r.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Ticket entity configuration
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.EventId);
            entity.HasIndex(t => t.QRCodeData).IsUnique();
            entity.Property(t => t.PurchaserEmail).IsRequired().HasMaxLength(255);
            entity.Property(t => t.PurchaserDNI).IsRequired().HasMaxLength(50);
            entity.Property(t => t.QRCodeData).IsRequired().HasMaxLength(500);
            entity.Property(t => t.IsUsed).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();

            entity.HasOne(t => t.Event)
                .WithMany(e => e.Tickets)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.TicketType)
                .WithMany()
                .HasForeignKey(t => t.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Transaction entity configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.ReservationId);
            entity.HasIndex(t => t.MercadoPagoId);
            entity.Property(t => t.MercadoPagoId).IsRequired().HasMaxLength(255);
            entity.Property(t => t.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(t => t.Status).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.Property(t => t.UpdatedAt).IsRequired();

            entity.HasOne(t => t.Reservation)
                .WithMany()
                .HasForeignKey(t => t.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog entity configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.ActionType);
            entity.HasIndex(a => a.Timestamp);
            entity.Property(a => a.ActionType)
                .IsRequired()
                .HasMaxLength(100)
                .HasConversion<string>();
            entity.Property(a => a.ResourceType)
                .IsRequired()
                .HasMaxLength(100)
                .HasConversion<string>();
            entity.Property(a => a.Details).HasMaxLength(1000);
            entity.Property(a => a.Timestamp).IsRequired();
        });
    }
}
