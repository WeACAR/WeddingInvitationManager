using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class QRScan
{
    public int Id { get; set; }

    [Required]
    public int InvitationId { get; set; }
    public Invitation Invitation { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string ScannedBy { get; set; } = string.Empty; // Guard name

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public ScanResult Result { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string? IpAddress { get; set; }
}

public enum ScanResult
{
    Valid = 1,
    AlreadyUsed = 2,
    Expired = 3,
    Invalid = 4,
    NotFound = 5
}
