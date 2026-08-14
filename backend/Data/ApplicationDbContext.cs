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
    public DbSet<PendingEmailSend> PendingEmailSends { get; set; }
    public DbSet<EventNotification> EventNotifications { get; set; }

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
            // EA-001/D-3: required int column; EF scaffolds defaultValue 0 (=Pending)
            // for the NOT NULL add on a populated table. No HasConversion/HasDefaultValue
            // (mirrors ReservationStatus/TransactionStatus).
            entity.Property(e => e.Status).IsRequired();

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
            entity.Property(t => t.CreatedAt).IsRequired();

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
            entity.Property(r => r.PurchaserEmail).HasMaxLength(255);
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
            entity.HasIndex(t => t.ReservationId);
            entity.Property(t => t.PurchaserEmail).IsRequired().HasMaxLength(255);
            entity.Property(t => t.PurchaserDNI).IsRequired().HasMaxLength(50);
            entity.Property(t => t.QRCodeData).IsRequired().HasMaxLength(500);
            entity.Property(t => t.IsUsed).IsRequired();
            entity.Property(t => t.IsRefunded).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();

            entity.HasOne(t => t.Event)
                .WithMany(e => e.Tickets)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.TicketType)
                .WithMany()
                .HasForeignKey(t => t.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // APR-009: nullable link from ticket to its confirmed purchase. Restrict
            // (not Cascade) so refunds never cascade-delete tickets.
            entity.HasOne(t => t.Reservation)
                .WithMany()
                .HasForeignKey(t => t.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Transaction entity configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.ReservationId);
            entity.HasIndex(t => t.MercadoPagoId).IsUnique();
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
            entity.Property(a => a.IpAddress).HasMaxLength(45);
            entity.Property(a => a.UserAgent).HasMaxLength(500);
            entity.Property(a => a.UserIdentifier).HasMaxLength(200);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // PendingEmailSend entity configuration
        modelBuilder.Entity<PendingEmailSend>(entity =>
        {
            entity.ToTable("pending_email_send");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReservationId);
            entity.Property(e => e.PaymentId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.RecipientEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TicketIds).HasColumnType("uuid[]");
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.Property(e => e.Attempts).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MaxAttempts).IsRequired().HasDefaultValue(5);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Reservation)
                .WithMany()
                .HasForeignKey(e => e.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EventNotification entity configuration
        modelBuilder.Entity<EventNotification>(entity =>
        {
            entity.ToTable("event_notifications");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.Property(e => e.EventName).IsRequired().HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.NotificationType).IsRequired().HasMaxLength(50).HasDefaultValue("DateChange");
            entity.Property(e => e.RecipientEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.Attempts).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MaxAttempts).IsRequired().HasDefaultValue(5);
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Event)
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
