using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class InvitationTemplate
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    public int QRPositionX { get; set; }
    public int QRPositionY { get; set; }
    public int QRSize { get; set; } = 100;

    public bool IsDefault { get; set; } = false;
    public bool IsVipTemplate { get; set; } = false;

    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
