using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using WeddingInvitationManager.Services;
using WeddingInvitationManager.Hubs;
using Microsoft.AspNetCore.Localization;

// Configure Npgsql to handle timestamps properly
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configure PostgreSQL for Supabase
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.MigrationsAssembly("WeddingInvitationManager")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews()
    .AddViewLocalization(options => options.ResourcesPath = "Resources")
    .AddDataAnnotationsLocalization(options => 
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(WeddingInvitationManager.Resources.SharedResource));
    });

// Add SignalR
builder.Services.AddSignalR();

// Add localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "ar-SA" };
    options.SetDefaultCulture("en-US")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    
    // Add cookie request culture provider
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// Add memory cache for high-performance scanning
builder.Services.AddMemoryCache();

// Add custom services
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IQRScanService, QRScanService>();
builder.Services.AddScoped<IContactImportService, ContactImportService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<HighPerformanceQRScanService>();
builder.Services.AddScoped<RoleInitializationService>();
builder.Services.AddScoped<LanguageService>();

// Add HttpContextAccessor for language service
builder.Services.AddHttpContextAccessor();

// Add HttpClient for WhatsApp service
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

// Configure file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

var app = builder.Build();

// Initialize roles and default admin
using (var scope = app.Services.CreateScope())
{
    var roleService = scope.ServiceProvider.GetRequiredService<RoleInitializationService>();
    await roleService.InitializeRolesAsync();
    await roleService.CreateDefaultAdminAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Configure request localization
var localizationOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure SignalR Hub
app.MapHub<QRScanHub>("/qrScanHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();
