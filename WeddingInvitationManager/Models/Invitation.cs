using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class Invitation
{
    public int Id { get; set; }

    [Required]
    public string QRCode { get; set; } = string.Empty;

    [Required]
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;

    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    [StringLength(500)]
    public string? ImagePath { get; set; }

    public bool IsSent { get; set; } = false;
    public DateTime? SentAt { get; set; }

    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }

    [StringLength(100)]
    public string? UsedByGuard { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Navigation properties
    public ICollection<QRScan> QRScans { get; set; } = new List<QRScan>();
}
