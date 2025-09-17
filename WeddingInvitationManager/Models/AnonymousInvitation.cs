using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class AnonymousInvitation
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string GuestName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string GuestNumber { get; set; } = string.Empty; // G001_0917 format

    [Required]
    [StringLength(100)]
    public string QRCode { get; set; } = string.Empty;

    public string? ImagePath { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    public string DownloadType { get; set; } = string.Empty; // "template" or "qr-only"

    public int BatchNumber { get; set; } = 1; // Track batches for management
}
