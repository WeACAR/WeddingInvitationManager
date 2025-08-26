using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models.ViewModels;

public class CreateEventViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Event Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Host Name")]
    public string Host { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Event Date")]
    public DateTime Date { get; set; } = DateTime.Now.AddDays(30);

    [StringLength(500)]
    [Display(Name = "Location Link")]
    [Url]
    public string? LocationLink { get; set; }

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}
