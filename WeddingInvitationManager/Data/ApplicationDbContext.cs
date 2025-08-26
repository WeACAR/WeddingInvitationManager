using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WeddingInvitationManager.Models;

namespace WeddingInvitationManager.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<QRScan> QRScans { get; set; }
    public DbSet<InvitationTemplate> InvitationTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Event configuration
        builder.Entity<Event>()
            .HasIndex(e => e.UserId);

        // Contact configuration
        builder.Entity<Contact>()
            .HasIndex(c => new { c.EventId, c.PhoneNumber })
            .IsUnique();

        // Invitation configuration
        builder.Entity<Invitation>()
            .HasIndex(i => i.QRCode)
            .IsUnique();

        builder.Entity<Invitation>()
            .HasIndex(i => i.EventId);

        // QRScan configuration
        builder.Entity<QRScan>()
            .HasIndex(q => q.InvitationId);

        builder.Entity<QRScan>()
            .HasIndex(q => q.ScannedAt);

        // InvitationTemplate configuration
        builder.Entity<InvitationTemplate>()
            .HasIndex(t => t.EventId);
    }
}
