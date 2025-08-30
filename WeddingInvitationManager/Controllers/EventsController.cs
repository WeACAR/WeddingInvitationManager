using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using WeddingInvitationManager.Models.ViewModels;
using WeddingInvitationManager.Services;

namespace WeddingInvitationManager.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IQRCodeService _qrCodeService;

    public EventsController(ApplicationDbContext context, IQRCodeService qrCodeService)
    {
        _context = context;
        _qrCodeService = qrCodeService;
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

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEventViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = new Event
            {
                Name = model.Name,
                Host = model.Host,
                Date = model.Date,
                LocationLink = model.LocationLink,
                Description = model.Description,
                UserId = userId!,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Events.Add(eventEntity);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Event created successfully!";
            return RedirectToAction(nameof(Details), new { id = eventEntity.Id });
        }

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Contacts)
            .Include(e => e.Invitations)
            .ThenInclude(i => i.Contact)
            .Include(e => e.Templates)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        return View(eventEntity);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        var model = new CreateEventViewModel
        {
            Name = eventEntity.Name,
            Host = eventEntity.Host,
            Date = eventEntity.Date,
            LocationLink = eventEntity.LocationLink,
            Description = eventEntity.Description
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CreateEventViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            eventEntity.Name = model.Name;
            eventEntity.Host = model.Host;
            eventEntity.Date = model.Date;
            eventEntity.LocationLink = model.LocationLink;
            eventEntity.Description = model.Description;
            eventEntity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Event updated successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        _context.Events.Remove(eventEntity);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Event deleted successfully!";
        return RedirectToAction(nameof(Index));
    }
    
    // Public invitation page (no authentication required)
    [AllowAnonymous]
    public async Task<IActionResult> PublicInvite(int id)
    {
        var eventEntity = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (eventEntity == null || eventEntity.Date < DateTime.Now.AddDays(-1))
            return NotFound("Event not found or has ended.");

        return View(eventEntity);
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> GetMyInvitation(int eventId, string phoneNumber)
    {
        try
        {
            // Clean phone number
            if (string.IsNullOrEmpty(phoneNumber))
                return Json(new { success = false, message = "Please enter a valid phone number." });
                
            phoneNumber = phoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            // Find contact by phone number
            var contact = await _context.Contacts
                .Include(c => c.Invitations)
                .FirstOrDefaultAsync(c => c.EventId == eventId && 
                    (c.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Contains(phoneNumber) ||
                     phoneNumber.Contains(c.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", ""))));

            if (contact == null)
                return Json(new { success = false, message = "Phone number not found in guest list." });

            // Get or create invitation
            var invitation = contact.Invitations.FirstOrDefault();
            if (invitation == null)
            {
                // This shouldn't happen normally, but create one if needed
                invitation = new Invitation
                {
                    ContactId = contact.Id,
                    EventId = eventId,
                    QRCode = _qrCodeService.GenerateUniqueQRCode(),
                    ExpiresAt = (await _context.Events.FindAsync(eventId))?.Date.AddDays(1) ?? DateTime.UtcNow.AddDays(1),
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Invitations.Add(invitation);
                await _context.SaveChangesAsync();
            }

            return Json(new { 
                success = true, 
                guestName = contact.Name,
                qrCode = invitation.QRCode,
                isVip = contact.IsVip,
                category = contact.Category,
                message = $"Welcome {contact.Name}! Here's your invitation QR code."
            });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred. Please try again." });
        }
    }

    public async Task<IActionResult> Dashboard(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Contacts)
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        var stats = new
        {
            TotalContacts = eventEntity.Contacts.Count,
            SentInvitations = eventEntity.Invitations.Count(i => i.IsSent),
            UsedInvitations = eventEntity.Invitations.Count(i => i.IsUsed),
            VipContacts = eventEntity.Contacts.Count(c => c.IsVip)
        };

        ViewBag.Stats = stats;
        return View(eventEntity);
    }
}
