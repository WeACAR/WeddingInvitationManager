using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Services;
using WeddingInvitationManager.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configure PostgreSQL for Supabase
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Add SignalR
builder.Services.AddSignalR();

// Add custom services
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IQRScanService, QRScanService>();
builder.Services.AddScoped<IContactImportService, ContactImportService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();

// Add HttpClient for WhatsApp service
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

// Configure file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

var app = builder.Build();

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
