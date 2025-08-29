using Microsoft.AspNetCore.Identity;

namespace WeddingInvitationManager.Models
{
    public static class RoleConstants
    {
        public const string Admin = "Admin";
        public const string Sender = "Sender";
        public const string Guard = "Guard";
    }

    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
