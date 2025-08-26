using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class Contact
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? Category { get; set; } // VIP, Family, Friends, etc.

    public bool IsVip { get; set; } = false;

    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
}
