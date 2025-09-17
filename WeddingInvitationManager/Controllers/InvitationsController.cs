using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading;
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

        // Check for unused anonymous invitations
        var unusedAnonymousInvitations = await _context.AnonymousInvitations
            .Where(ai => ai.EventId == eventId && !ai.IsUsed)
            .GroupBy(ai => ai.DownloadType)
            .Select(g => new { DownloadType = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.UnusedAnonymousInvitations = unusedAnonymousInvitations;

        // Also provide data for warning message compatibility
        if (unusedAnonymousInvitations.Any())
        {
            var totalUnused = unusedAnonymousInvitations.Sum(g => g.Count);
            var downloadTypes = string.Join(", ", unusedAnonymousInvitations.Select(g => 
                g.DownloadType.StartsWith("template:") ? "template" : g.DownloadType));
            
            ViewBag.HasUnusedInvitations = true;
            ViewBag.UnusedInvitationsWarning = $"You have {totalUnused} unused anonymous invitations ({downloadTypes}) for this event.";
            ViewBag.UnusedInvitationsList = unusedAnonymousInvitations;
        }
        else
        {
            ViewBag.HasUnusedInvitations = false;
        }

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
        
        // Get anonymous invitations count for unused warning
        var anonymousInvitations = await _context.AnonymousInvitations
            .Where(ai => ai.EventId == eventId)
            .ToListAsync();
        ViewBag.AnonymousInvitations = anonymousInvitations;

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadAllInvitations(int eventId, string downloadType, int? templateId = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .Include(e => e.Contacts)
                .Include(e => e.Templates)
                .Include(e => e.Invitations)
                .ThenInclude(i => i.Contact)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound("Event not found or you don't have access to it.");

            if (!eventEntity.Contacts.Any())
            {
                TempData["Error"] = "No contacts found for this event. Please add contacts first.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            if (downloadType == "template" && !eventEntity.Templates.Any())
            {
                TempData["Error"] = "No templates found for this event. Please create a template first.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            string zipFileName;
            byte[] zipBytes;

            if (downloadType == "qr-only")
            {
                // Generate just QR codes for all contacts
                var qrImages = new List<string>();
                var qrDir = Path.Combine(_environment.WebRootPath, "generated", "qrcodes");
                Directory.CreateDirectory(qrDir);

                foreach (var contact in eventEntity.Contacts)
                {
                    // Generate unique QR code for this contact
                    var qrCode = _qrCodeService.GenerateUniqueQRCode();
                    var qrImageBytes = _qrCodeService.GenerateQRCodeImage(qrCode, 300);
                    
                    var qrFileName = $"QR_{contact.Name.Replace(" ", "_")}_{contact.PhoneNumber}.png";
                    var qrPath = Path.Combine(qrDir, qrFileName);
                    await System.IO.File.WriteAllBytesAsync(qrPath, qrImageBytes);
                    qrImages.Add($"/generated/qrcodes/{qrFileName}");
                }

                zipBytes = await _qrCodeService.CreateInvitationZipAsync(qrImages, $"{eventEntity.Name}_QR_Codes");
                zipFileName = $"{eventEntity.Name}_QR_Codes_{DateTime.UtcNow:yyyyMMdd}.zip";
            }
            else
            {
                // Generate full invitation templates with QR codes
                var imagePaths = await _qrCodeService.CreateIndividualInvitationsAsync(eventId, templateId);
                
                if (imagePaths == null || !imagePaths.Any())
                {
                    TempData["Error"] = "No invitation images were generated. Please check that contacts and templates exist.";
                    return RedirectToAction("Details", "Events", new { id = eventId });
                }

                zipBytes = await _qrCodeService.CreateInvitationZipAsync(imagePaths, eventEntity.Name);
                zipFileName = $"{eventEntity.Name}_All_Invitations_{DateTime.UtcNow:yyyyMMdd}.zip";
            }

            return File(zipBytes, "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DownloadAllInvitations: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            TempData["Error"] = $"Error creating invitation package: {ex.Message}";
            return RedirectToAction("Details", "Events", new { id = eventId });
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
                //return RedirectToAction("Details", "Events", new { id = eventId });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateAnonymousInvitations(int eventId, int guestCount, string downloadType, int? templateId = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .Include(e => e.Templates)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound("Event not found or you don't have access to it.");

            if (guestCount <= 0 || guestCount > 500)
            {
                TempData["Error"] = "Guest count must be between 1 and 500.";
                return RedirectToAction(nameof(Templates), new { eventId });
            }

            if (downloadType == "template" && !eventEntity.Templates.Any())
            {
                TempData["Error"] = "No templates found for this event. Please create a template first.";
                return RedirectToAction(nameof(Templates), new { eventId });
            }

            // Always generate new invitations - no check for existing unused ones
            return await ProcessAnonymousInvitationGeneration(eventId, guestCount, downloadType, templateId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GenerateAnonymousInvitations: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            TempData["Error"] = $"Error creating anonymous invitations: {ex.Message}";
            return RedirectToAction(nameof(Templates), new { eventId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadUnusedAnonymousInvitations(int eventId, string downloadType)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound("Event not found or you don't have access to it.");

            // Filter by download type - handle template types that include template ID
            var existingInvitations = await _context.AnonymousInvitations
                .Where(a => a.EventId == eventId && !a.IsUsed && 
                       (a.DownloadType == downloadType || 
                        (downloadType == "template" && a.DownloadType.StartsWith("template:"))))
                .OrderBy(a => a.BatchNumber)
                .ThenBy(a => a.GuestNumber)
                .ToListAsync();

            if (!existingInvitations.Any())
            {
                TempData["Error"] = $"No existing unused {downloadType} anonymous invitations found.";
                return RedirectToAction(nameof(Templates), new { eventId });
            }

            var imagePaths = existingInvitations
                .Where(inv => !string.IsNullOrEmpty(inv.ImagePath))
                .Select(inv => inv.ImagePath!)
                .ToList();

            if (!imagePaths.Any())
            {
                TempData["Error"] = "No image files found for existing invitations.";
                return RedirectToAction(nameof(Templates), new { eventId });
            }

            var zipPrefix = downloadType == "qr-only" ? "QR_Codes" : "Invitations";
            var zipBytes = await _qrCodeService.CreateInvitationZipAsync(imagePaths, $"{eventEntity.Name}_Unused_{zipPrefix}");
            var zipFileName = $"{eventEntity.Name}_Unused_{zipPrefix}_{existingInvitations.Count}_{DateTime.UtcNow:yyyyMMdd}.zip";

            return File(zipBytes, "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error downloading unused invitations: {ex.Message}";
            return RedirectToAction(nameof(Templates), new { eventId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmNewAnonymousInvitations(int eventId, int guestCount, string downloadType, int? templateId = null)
    {
        try
        {
            return await ProcessAnonymousInvitationGeneration(eventId, guestCount, downloadType, templateId);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error creating anonymous invitations: {ex.Message}";
            return RedirectToAction(nameof(Send), new { eventId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadExistingAnonymousInvitations(int eventId, string downloadType)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound("Event not found or you don't have access to it.");

            var existingInvitations = await _context.AnonymousInvitations
                .Where(a => a.EventId == eventId && !a.IsUsed && a.DownloadType == downloadType)
                .OrderBy(a => a.BatchNumber)
                .ThenBy(a => a.GuestNumber)
                .ToListAsync();

            if (!existingInvitations.Any())
            {
                TempData["Error"] = $"No existing {downloadType} anonymous invitations found.";
                return RedirectToAction(nameof(Send), new { eventId });
            }

            var imagePaths = existingInvitations
                .Where(inv => !string.IsNullOrEmpty(inv.ImagePath))
                .Select(inv => inv.ImagePath!)
                .ToList();

            if (!imagePaths.Any())
            {
                TempData["Error"] = "No image files found for existing invitations.";
                return RedirectToAction(nameof(Send), new { eventId });
            }

            var zipPrefix = downloadType == "qr-only" ? "QR_Codes" : "Invitations";
            var zipBytes = await _qrCodeService.CreateInvitationZipAsync(imagePaths, $"{eventEntity.Name}_Anonymous_{zipPrefix}");
            var zipFileName = $"{eventEntity.Name}_Anonymous_{zipPrefix}_Existing_{DateTime.UtcNow:yyyyMMdd}.zip";

            return File(zipBytes, "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error downloading existing invitations: {ex.Message}";
            return RedirectToAction(nameof(Send), new { eventId });
        }
    }

    private async Task<IActionResult> ProcessAnonymousInvitationGeneration(int eventId, int guestCount, string downloadType, int? templateId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Templates)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound("Event not found or you don't have access to it.");

        // Get the selected template or default (only needed for template downloads)
        var template = templateId.HasValue 
            ? eventEntity.Templates.FirstOrDefault(t => t.Id == templateId.Value)
            : eventEntity.Templates.FirstOrDefault(t => t.IsDefault) ?? eventEntity.Templates.FirstOrDefault();

        var imagePaths = new List<string>();
        var invitationsDir = Path.Combine(_environment.WebRootPath, "generated", "invitations");
        var qrDir = Path.Combine(_environment.WebRootPath, "generated", "qrcodes");
        Directory.CreateDirectory(invitationsDir);
        Directory.CreateDirectory(qrDir);

        // Get current culture for localization
        var currentCulture = Thread.CurrentThread.CurrentCulture.Name;
        var isArabic = currentCulture.StartsWith("ar");

        // Get next batch number
        var lastBatch = await _context.AnonymousInvitations
            .Where(a => a.EventId == eventId)
            .MaxAsync(a => (int?)a.BatchNumber) ?? 0;
        var currentBatch = lastBatch + 1;

        // Create anonymous invitations in the new table
        var anonymousInvitations = new List<AnonymousInvitation>();
        
        for (int i = 1; i <= guestCount; i++)
        {
            try
            {
                // Create guest name based on language
                var guestName = isArabic ? $"ضيف {i}" : $"Guest {i}";
                var guestNumber = $"G{i:D3}_{currentBatch:D2}"; // G001_01, G002_01, etc.
                
                // Generate unique QR code for anonymous guest
                var qrCode = _qrCodeService.GenerateUniqueQRCode();
                
                // Store download type with template ID if applicable
                var downloadTypeWithTemplate = downloadType;
                if (downloadType == "template" && templateId.HasValue)
                {
                    downloadTypeWithTemplate = $"template:{templateId.Value}";
                }
                else if (downloadType == "template" && template != null)
                {
                    downloadTypeWithTemplate = $"template:{template.Id}";
                }
                
                // Create anonymous invitation record
                var anonymousInvitation = new AnonymousInvitation
                {
                    GuestName = guestName,
                    GuestNumber = guestNumber,
                    QRCode = qrCode, // Store just the code for database lookup
                    EventId = eventId,
                    IsUsed = false,
                    ExpiresAt = eventEntity.Date.AddDays(1), // Expire 1 day after event
                    DownloadType = downloadTypeWithTemplate, // Store download type with template ID
                    BatchNumber = currentBatch,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _context.AnonymousInvitations.Add(anonymousInvitation);
                await _context.SaveChangesAsync(); // Save to get the ID
                
                anonymousInvitations.Add(anonymousInvitation);
                
                if (downloadType == "qr-only")
                {
                    // Generate QR code image with just the code
                    var qrImageBytes = _qrCodeService.GenerateQRCodeImage(qrCode, 300);
                    var qrFileName = $"Anonymous_QR_{guestName.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{i:D3}.png";
                    var qrPath = Path.Combine(qrDir, qrFileName);
                    await System.IO.File.WriteAllBytesAsync(qrPath, qrImageBytes);
                    
                    var qrImagePath = $"/generated/qrcodes/{qrFileName}";
                    imagePaths.Add(qrImagePath);
                    
                    // Update with image path
                    anonymousInvitation.ImagePath = qrImagePath;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Generate invitation image with template using just the code
                    var imagePath = await _qrCodeService.CreateInvitationImageAsync(
                        template?.ImagePath ?? "", 
                        qrCode, // Use just the code
                        template?.QRPositionX ?? 100, 
                        template?.QRPositionY ?? 100, 
                        template?.QRSize ?? 150
                    );

                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        // Rename the file to include guest name
                        var originalPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                        var fileName = $"Anonymous_{guestName.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{i:D3}.png";
                        var newPath = Path.Combine(invitationsDir, fileName);
                        
                        if (System.IO.File.Exists(originalPath))
                        {
                            System.IO.File.Move(originalPath, newPath);
                            var finalImagePath = $"/generated/invitations/{fileName}";
                            imagePaths.Add(finalImagePath);
                            
                            // Update invitation with image path
                            anonymousInvitation.ImagePath = finalImagePath;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating invitation for anonymous guest {i}: {ex.Message}");
            }
        }

        if (!imagePaths.Any())
        {
            TempData["Error"] = "Failed to generate any anonymous invitations. Please try again.";
            return RedirectToAction(nameof(Send), new { eventId });
        }

        // Log successful creation
        Console.WriteLine($"Successfully created {anonymousInvitations.Count} anonymous invitations in database for scanning");
        foreach (var inv in anonymousInvitations.Take(5)) // Log first 5 for verification
        {
            Console.WriteLine($"  - QR Code: {inv.QRCode}, Guest: {inv.GuestName}");
        }

        // Create ZIP file with appropriate naming
        var zipPrefix = downloadType == "qr-only" ? "QR_Codes" : "Invitations";
        var zipBytes = await _qrCodeService.CreateInvitationZipAsync(imagePaths, $"{eventEntity.Name}_Anonymous_{zipPrefix}");
        var zipFileName = $"{eventEntity.Name}_Anonymous_{zipPrefix}_{guestCount}_{DateTime.UtcNow:yyyyMMdd}.zip";

        return File(zipBytes, "application/zip", zipFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadAllInvitationsWithAnonymous(int eventId, string downloadType, int? templateId = null, int anonymousGuestCount = 0)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .Include(e => e.Contacts)
                .Include(e => e.Templates)
                .Include(e => e.Invitations)
                .ThenInclude(i => i.Contact)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound("Event not found or you don't have access to it.");

            if (!eventEntity.Contacts.Any() && anonymousGuestCount <= 0)
            {
                TempData["Error"] = "No contacts found and no anonymous guests specified. Please add contacts or specify anonymous guest count.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            if (downloadType == "template" && !eventEntity.Templates.Any())
            {
                TempData["Error"] = "No templates found for this event. Please create a template first.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            var allImagePaths = new List<string>();
            string zipFileName;

            // Get current culture for localization
            var currentCulture = Thread.CurrentThread.CurrentCulture.Name;
            var isArabic = currentCulture.StartsWith("ar");

            if (downloadType == "qr-only")
            {
                // Generate QR codes for known contacts
                var qrDir = Path.Combine(_environment.WebRootPath, "generated", "qrcodes");
                Directory.CreateDirectory(qrDir);

                foreach (var contact in eventEntity.Contacts)
                {
                    var qrCode = _qrCodeService.GenerateUniqueQRCode();
                    var qrImageBytes = _qrCodeService.GenerateQRCodeImage(qrCode, 300);
                    
                    var qrFileName = $"QR_{contact.Name.Replace(" ", "_")}_{contact.PhoneNumber}.png";
                    var qrPath = Path.Combine(qrDir, qrFileName);
                    await System.IO.File.WriteAllBytesAsync(qrPath, qrImageBytes);
                    allImagePaths.Add($"/generated/qrcodes/{qrFileName}");
                }

                // Generate QR codes for anonymous guests using AnonymousInvitation table
                if (anonymousGuestCount > 0)
                {
                    var maxBatch = await _context.AnonymousInvitations
                        .Where(ai => ai.EventId == eventId)
                        .MaxAsync(ai => (int?)ai.BatchNumber) ?? 0;
                    var newBatchNumber = maxBatch + 1;

                    for (int i = 1; i <= anonymousGuestCount; i++)
                    {
                        // Create guest name based on language
                        var guestName = isArabic ? $"ضيف {i}" : $"Guest {i}";
                        
                        var qrCode = _qrCodeService.GenerateUniqueQRCode();
                        
                        // Create anonymous invitation record
                        var anonymousInvitation = new AnonymousInvitation
                        {
                            EventId = eventId,
                            QRCode = qrCode,
                            GuestName = guestName,
                            GuestNumber = i.ToString(),
                            BatchNumber = newBatchNumber,
                            DownloadType = downloadType,
                            IsUsed = false,
                            ExpiresAt = eventEntity.Date.AddDays(1),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        
                        _context.AnonymousInvitations.Add(anonymousInvitation);
                        await _context.SaveChangesAsync();
                        
                        var qrImageBytes = _qrCodeService.GenerateQRCodeImage(qrCode, 300);
                        var qrFileName = $"QR_Anonymous_{guestName.Replace(" ", "_")}.png";
                        var qrPath = Path.Combine(qrDir, qrFileName);
                        await System.IO.File.WriteAllBytesAsync(qrPath, qrImageBytes);
                        allImagePaths.Add($"/generated/qrcodes/{qrFileName}");
                    }
                }

                zipFileName = $"{eventEntity.Name}_All_QR_Codes_{DateTime.UtcNow:yyyyMMdd}.zip";
            }
            else
            {
                // Generate full invitation templates for known contacts
                if (eventEntity.Contacts.Any())
                {
                    var knownContactImages = await _qrCodeService.CreateIndividualInvitationsAsync(eventId, templateId);
                    if (knownContactImages != null)
                        allImagePaths.AddRange(knownContactImages);
                }

                // Generate full invitation templates for anonymous guests using AnonymousInvitation table
                if (anonymousGuestCount > 0)
                {
                    var template = templateId.HasValue 
                        ? eventEntity.Templates.FirstOrDefault(t => t.Id == templateId.Value)
                        : eventEntity.Templates.FirstOrDefault(t => t.IsDefault) ?? eventEntity.Templates.First();

                    var invitationsDir = Path.Combine(_environment.WebRootPath, "generated", "invitations");
                    Directory.CreateDirectory(invitationsDir);

                    var maxBatch = await _context.AnonymousInvitations
                        .Where(ai => ai.EventId == eventId)
                        .MaxAsync(ai => (int?)ai.BatchNumber) ?? 0;
                    var newBatchNumber = maxBatch + 1;

                    for (int i = 1; i <= anonymousGuestCount; i++)
                    {
                        // Create guest name based on language
                        var guestName = isArabic ? $"ضيف {i}" : $"Guest {i}";
                        
                        var qrCode = _qrCodeService.GenerateUniqueQRCode();
                        
                        // Create anonymous invitation record
                        var anonymousInvitation = new AnonymousInvitation
                        {
                            EventId = eventId,
                            QRCode = qrCode,
                            GuestName = guestName,
                            GuestNumber = i.ToString(),
                            BatchNumber = newBatchNumber,
                            DownloadType = downloadType,
                            IsUsed = false,
                            ExpiresAt = eventEntity.Date.AddDays(1),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        
                        _context.AnonymousInvitations.Add(anonymousInvitation);
                        await _context.SaveChangesAsync();
                        
                        var imagePath = await _qrCodeService.CreateInvitationImageAsync(
                            template?.ImagePath ?? "", 
                            qrCode, 
                            template?.QRPositionX ?? 100, 
                            template?.QRPositionY ?? 100, 
                            template?.QRSize ?? 150
                        );

                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            var originalPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                            var fileName = $"Anonymous_{guestName.Replace(" ", "_")}_{i:D3}.png";
                            var newPath = Path.Combine(invitationsDir, fileName);
                            
                            if (System.IO.File.Exists(originalPath))
                            {
                                System.IO.File.Move(originalPath, newPath);
                                allImagePaths.Add($"/generated/invitations/{fileName}");
                                
                                // Update anonymous invitation with image path
                                anonymousInvitation.ImagePath = $"/generated/invitations/{fileName}";
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }

                zipFileName = $"{eventEntity.Name}_All_Invitations_With_Anonymous_{DateTime.UtcNow:yyyyMMdd}.zip";
            }

            if (!allImagePaths.Any())
            {
                TempData["Error"] = "No invitation images were generated. Please check that contacts exist or anonymous guest count is specified.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            var zipBytes = await _qrCodeService.CreateInvitationZipAsync(allImagePaths, eventEntity.Name);
            return File(zipBytes, "application/zip", zipFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DownloadAllInvitationsWithAnonymous: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            TempData["Error"] = $"Error creating invitation package: {ex.Message}";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }
    }
}
