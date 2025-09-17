using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class QRScan
{
    public int Id { get; set; }

    // For regular invitations
    public int? InvitationId { get; set; }
    public Invitation? Invitation { get; set; }

    // For anonymous invitations
    public int? AnonymousInvitationId { get; set; }
    public AnonymousInvitation? AnonymousInvitation { get; set; }

    [Required]
    [StringLength(100)]
    public string ScannedBy { get; set; } = string.Empty; // Guard name

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public ScanResult Result { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string? IpAddress { get; set; }

    // For easy access to guest information regardless of invitation type
    [StringLength(200)]
    public string GuestName { get; set; } = string.Empty;

    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    public bool IsVip { get; set; }

    [Required]
    public int EventId { get; set; }
}

public enum ScanResult
{
    Valid = 1,
    AlreadyUsed = 2,
    Expired = 3,
    Invalid = 4,
    NotFound = 5
}
