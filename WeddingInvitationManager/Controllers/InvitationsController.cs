using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using WeddingInvitationManager.Services;
using WeddingInvitationManager.Hubs;
using System.IO.Compression;

namespace WeddingInvitationManager.Controllers;

[Authorize]
public class InvitationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IQRCodeService _qrCodeService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IWebHostEnvironment _environment;
    private readonly IHubContext<QRScanHub> _hubContext;

    public InvitationsController(
        ApplicationDbContext context,
        IQRCodeService qrCodeService,
        IWhatsAppService whatsAppService,
        IWebHostEnvironment environment,
        IHubContext<QRScanHub> hubContext)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _whatsAppService = whatsAppService;
        _environment = environment;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var events = await _context.Events
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return View(events);
    }

    public async Task<IActionResult> Templates(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Templates)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        return View(eventEntity);
    }

    public async Task<IActionResult> CreateTemplate(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        ViewBag.EventId = eventId;
        ViewBag.EventName = eventEntity.Name;
        return View();
    }

    public async Task<IActionResult> CreateTemplateEnhanced(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        ViewBag.EventId = eventId;
        ViewBag.EventName = eventEntity.Name;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(int eventId, IFormFile imageFile, string templateName, 
        int qrPositionX, int qrPositionY, int qrSize, bool isDefault, bool isVipTemplate)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            if (imageFile == null || imageFile.Length == 0)
            {
                TempData["Error"] = "Please select an image file.";
                return RedirectToAction(nameof(CreateTemplate), new { eventId });
            }

            // Save uploaded image
            var fileName = $"{Guid.NewGuid()}_{imageFile.FileName}";
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "images");
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // If this is default, unset other defaults
            if (isDefault)
            {
                var existingDefaults = await _context.InvitationTemplates
                    .Where(t => t.EventId == eventId && t.IsDefault)
                    .ToListAsync();
                
                foreach (var template in existingDefaults)
                {
                    template.IsDefault = false;
                }
            }

            // Create template
            var newTemplate = new InvitationTemplate
            {
                Name = templateName,
                ImagePath = $"/uploads/images/{fileName}",
                QRPositionX = qrPositionX,
                QRPositionY = qrPositionY,
                QRSize = qrSize,
                IsDefault = isDefault,
                IsVipTemplate = isVipTemplate,
                EventId = eventId
            };

            _context.InvitationTemplates.Add(newTemplate);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Template created successfully!";
            return RedirectToAction(nameof(Templates), new { eventId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error creating template: {ex.Message}";
            return RedirectToAction(nameof(CreateTemplate), new { eventId });
        }
    }

    public async Task<IActionResult> Send(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Contacts)
            .Include(e => e.Templates)
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        var stats = await _whatsAppService.GetSendStatsAsync(eventId);
        ViewBag.Stats = stats;

        return View(eventEntity);
    }

    public async Task<IActionResult> GenerateAdvanced(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Contacts)
            .Include(e => e.Templates)
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        return View(eventEntity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendBulk(int eventId, int? templateId = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            // Start bulk sending in background
            _ = Task.Run(async () =>
            {
                var results = await _whatsAppService.SendBulkInvitationsAsync(eventId, templateId);
                
                // Notify clients about completion
                await _hubContext.NotifyInvitationSentAsync(eventId, new
                {
                    TotalSent = results.Count(r => r.Success),
                    TotalFailed = results.Count(r => !r.Success),
                    Completed = true
                });
            });

            TempData["Info"] = "Bulk invitation sending started. You'll receive updates in real-time.";
            return RedirectToAction(nameof(Send), new { eventId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error starting bulk send: {ex.Message}";
            return RedirectToAction(nameof(Send), new { eventId });
        }
    }

    public async Task<IActionResult> Download(int eventId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .Include(e => e.Invitations)
                .ThenInclude(i => i.Contact)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            var sentInvitations = eventEntity.Invitations.Where(i => i.IsSent && !string.IsNullOrEmpty(i.ImagePath)).ToList();

            if (!sentInvitations.Any())
            {
                TempData["Error"] = "No sent invitations found to download.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            // Create zip file
            var zipFileName = $"{eventEntity.Name}_Invitations_{DateTime.UtcNow:yyyyMMdd}.zip";
            var zipPath = Path.Combine(_environment.WebRootPath, "uploads", "temp", zipFileName);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var invitation in sentInvitations)
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, invitation.ImagePath!.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        var fileName = $"{invitation.Contact.Name}_{invitation.Contact.PhoneNumber}.png";
                        archive.CreateEntryFromFile(imagePath, fileName);
                    }
                }
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            
            // Clean up temp file
            System.IO.File.Delete(zipPath);

            return File(fileBytes, "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error creating download: {ex.Message}";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }
    }

    public async Task<IActionResult> DownloadTemplates(int eventId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .Include(e => e.Templates)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            if (!eventEntity.Templates.Any())
            {
                TempData["Error"] = "No templates found to download.";
                return RedirectToAction(nameof(Templates), new { eventId });
            }

            // Create zip file with templates
            var zipFileName = $"{eventEntity.Name}_Templates_{DateTime.UtcNow:yyyyMMdd}.zip";
            var zipPath = Path.Combine(_environment.WebRootPath, "uploads", "temp", zipFileName);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var template in eventEntity.Templates)
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, template.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        var fileName = $"{template.Name}.png";
                        archive.CreateEntryFromFile(imagePath, fileName);
                    }
                }
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            
            // Clean up temp file
            System.IO.File.Delete(zipPath);

            return File(fileBytes, "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error creating template download: {ex.Message}";
            return RedirectToAction(nameof(Templates), new { eventId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(int id, int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var template = await _context.InvitationTemplates
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == id && t.Event.UserId == userId);

        if (template == null)
            return NotFound();

        // Delete image file
        var imagePath = Path.Combine(_environment.WebRootPath, template.ImagePath.TrimStart('/'));
        if (System.IO.File.Exists(imagePath))
        {
            System.IO.File.Delete(imagePath);
        }

        _context.InvitationTemplates.Remove(template);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Template deleted successfully!";
        return RedirectToAction(nameof(Templates), new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> GetSendProgress(int eventId)
    {
        try
        {
            var stats = await _whatsAppService.GetSendStatsAsync(eventId);
            return Json(stats);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    // New methods for enhanced invitation generation
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateGeneral(int eventId, int templateId, string eventUrl)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
            
            if (eventEntity == null)
                return NotFound();

            var template = await _context.InvitationTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.EventId == eventId);
            
            if (template == null)
                return BadRequest("Template not found");

            var imagePath = await _qrCodeService.CreateGeneralInvitationAsync(
                template.ImagePath, 
                eventUrl, 
                template.QRPositionX, 
                template.QRPositionY, 
                template.QRSize);

            return Json(new { success = true, imagePath });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateIndividual(int eventId, int? templateId = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
            
            if (eventEntity == null)
                return NotFound();

            var imagePaths = await _qrCodeService.CreateIndividualInvitationsAsync(eventId, templateId);
            
            return Json(new { success = true, count = imagePaths.Count, imagePaths });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> PreviewTemplate(int templateId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var template = await _context.InvitationTemplates
                .Include(t => t.Event)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.Event.UserId == userId);
            
            if (template == null)
                return NotFound();

            var previewPath = await _qrCodeService.CreatePreviewWithSampleQRAsync(
                template.ImagePath, 
                template.QRPositionX, 
                template.QRPositionY, 
                template.QRSize);

            return Json(new { success = true, previewPath });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadIndividualZip(int eventId, int? templateId = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .Include(e => e.Contacts)
                .Include(e => e.Templates)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
            
            if (eventEntity == null)
                return NotFound("Event not found or you don't have access to it.");

            Console.WriteLine($"Event found: {eventEntity.Name}, Contacts count: {eventEntity.Contacts.Count}, Templates count: {eventEntity.Templates.Count}");

            if (!eventEntity.Contacts.Any())
            {
                TempData["Error"] = "No contacts found for this event. Please add contacts first.";
                return RedirectToAction(nameof(GenerateAdvanced), new { eventId });
            }

            if (!eventEntity.Templates.Any())
            {
                TempData["Error"] = "No templates found for this event. Please create a template first.";
                return RedirectToAction(nameof(GenerateAdvanced), new { eventId });
            }

            // Get existing invitations or create new ones if they don't exist
            var imagePaths = await _qrCodeService.CreateIndividualInvitationsAsync(eventId, templateId);
            
            Console.WriteLine($"Image paths generated: {imagePaths?.Count ?? 0}");
            
            if (imagePaths == null || !imagePaths.Any())
            {
                TempData["Error"] = "No invitation images were generated. Please check that contacts and templates exist.";
                return RedirectToAction(nameof(GenerateAdvanced), new { eventId });
            }

            var zipBytes = await _qrCodeService.CreateInvitationZipAsync(imagePaths, eventEntity.Name);
            
            var fileName = $"{eventEntity.Name}_Individual_Invitations_{DateTime.UtcNow:yyyyMMdd}.zip";
            return File(zipBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"Error in DownloadIndividualZip: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            TempData["Error"] = $"Error creating invitation package: {ex.Message}";
            return RedirectToAction(nameof(GenerateAdvanced), new { eventId });
        }
    }
}
