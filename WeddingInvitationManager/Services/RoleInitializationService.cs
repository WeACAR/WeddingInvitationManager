using Microsoft.AspNetCore.Identity;
using WeddingInvitationManager.Models;

namespace WeddingInvitationManager.Services
{
    public class RoleInitializationService
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RoleInitializationService> _logger;

        public RoleInitializationService(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            ILogger<RoleInitializationService> logger)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task InitializeRolesAsync()
        {
            string[] roles = { RoleConstants.Admin, RoleConstants.Sender, RoleConstants.Guard };

            foreach (string role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                    _logger.LogInformation($"Created role: {role}");
                }
            }
        }

        public async Task CreateDefaultAdminAsync()
        {
            var adminEmail = "admin@wedding.com";
            var adminUser = await _userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, RoleConstants.Admin);
                    _logger.LogInformation($"Created default admin user: {adminEmail}");
                }
                else
                {
                    _logger.LogError($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
