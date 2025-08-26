using System.ComponentModel.DataAnnotations;

namespace WeddingInvitationManager.Models.ViewModels;

public class ContactImportViewModel
{
    [Required]
    public int EventId { get; set; }

    [Display(Name = "Upload Contacts File")]
    public IFormFile? ContactsFile { get; set; }

    [Display(Name = "Contact Format")]
    public ContactFileFormat Format { get; set; } = ContactFileFormat.CSV;

    public List<ContactRowViewModel> ManualContacts { get; set; } = new();
}

public class ContactRowViewModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsVip { get; set; } = false;
}

public enum ContactFileFormat
{
    CSV = 1,
    Excel = 2,
    VCard = 3
}
