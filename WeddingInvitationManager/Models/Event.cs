using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models;

public class Event
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Host { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    [StringLength(500)]
    public string? LocationLink { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<InvitationTemplate> Templates { get; set; } = new List<InvitationTemplate>();
}
