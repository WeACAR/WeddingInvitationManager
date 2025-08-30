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
public class ContactsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IContactImportService _contactImportService;

    public ContactsController(ApplicationDbContext context, IContactImportService contactImportService)
    {
        _context = context;
        _contactImportService = contactImportService;
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

    public async Task<IActionResult> Manage(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .Include(e => e.Contacts)
                .ThenInclude(c => c.Invitations)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        ViewBag.EventName = eventEntity.Name;
        ViewBag.EventId = eventId;
        
        return View(eventEntity.Contacts.OrderBy(c => c.Name).ToList());
    }

    public async Task<IActionResult> Import(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        var model = new ContactImportViewModel
        {
            EventId = eventId,
            ManualContacts = new List<ContactRowViewModel> { new() }
        };

        ViewBag.EventName = eventEntity.Name;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportFile(ContactImportViewModel model)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == model.EventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            if (model.ContactsFile != null && model.ContactsFile.Length > 0)
            {
                // Log file details for debugging
                Console.WriteLine($"Importing file: {model.ContactsFile.FileName}, Size: {model.ContactsFile.Length} bytes, Format: {model.Format}");
                
                var contacts = await _contactImportService.ImportFromFileAsync(
                    model.ContactsFile, 
                    model.EventId, 
                    model.Format);

                if (contacts.Count > 0)
                {
                    TempData["Success"] = $"Successfully imported {contacts.Count} contacts!";
                }
                else
                {
                    TempData["Error"] = "No valid contacts were found in the file. Please check the file format and ensure it contains valid contact information with names and phone numbers.";
                }
            }
            else
            {
                TempData["Error"] = "Please select a file to import.";
            }

            return RedirectToAction("Details", "Events", new { id = model.EventId });
        }
        catch (Exception ex)
        {
            // Log the full exception for debugging
            Console.WriteLine($"Import error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            TempData["Error"] = $"Error importing contacts: {ex.Message}";
            return RedirectToAction(nameof(Import), new { eventId = model.EventId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportManual(ContactImportViewModel model)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == model.EventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            var validContacts = model.ManualContacts
                .Where(c => !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.PhoneNumber))
                .ToList();

            if (validContacts.Any())
            {
                var contacts = await _contactImportService.ImportFromManualEntryAsync(
                    validContacts, 
                    model.EventId);

                TempData["Success"] = $"Successfully added {contacts.Count} contacts!";
            }
            else
            {
                TempData["Error"] = "Please add at least one valid contact.";
            }

            return RedirectToAction("Details", "Events", new { id = model.EventId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error adding contacts: {ex.Message}";
            return RedirectToAction(nameof(Import), new { eventId = model.EventId });
        }
    }

    public async Task<IActionResult> Export(int eventId, string format = "csv")
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

            if (eventEntity == null)
                return NotFound();

            var fileBytes = await _contactImportService.ExportContactsAsync(eventId, format);
            var fileName = $"{eventEntity.Name}_contacts_{DateTime.UtcNow:yyyyMMdd}.{format}";
            var contentType = format.ToLower() == "excel" 
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv";

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error exporting contacts: {ex.Message}";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }
    }

    public async Task<IActionResult> Template(string format = "csv")
    {
        try
        {
            var templatePath = await _contactImportService.GenerateContactTemplateAsync(format);
            var fileName = $"contact_template.{format}";
            var contentType = format.ToLower() == "excel"
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv";

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", templatePath.TrimStart('/'));
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error generating template: {ex.Message}";
            return RedirectToAction("Index", "Events");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var contact = await _context.Contacts
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == id && c.Event.UserId == userId);

        if (contact == null)
            return NotFound();

        _context.Contacts.Remove(contact);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Contact deleted successfully!";
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    public async Task<IActionResult> AddRow()
    {
        return PartialView("_ContactRowPartial", new ContactRowViewModel());
    }

    // GET: Edit contact
    public async Task<IActionResult> Edit(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var contact = await _context.Contacts
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == id && c.Event.UserId == userId);

        if (contact == null)
        {
            return NotFound();
        }

        return View(contact);
    }

    // POST: Edit contact
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Contact contact)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        if (id != contact.Id)
        {
            return NotFound();
        }

        var existingContact = await _context.Contacts
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == id && c.Event.UserId == userId);

        if (existingContact == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                existingContact.Name = contact.Name;
                existingContact.PhoneNumber = contact.PhoneNumber;
                existingContact.Email = contact.Email;
                existingContact.Category = contact.Category;
                existingContact.IsVip = contact.IsVip;

                _context.Update(existingContact);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Contact updated successfully!";
                return RedirectToAction("Manage", new { eventId = existingContact.EventId });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ContactExists(contact.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        return View(contact);
    }

    // AJAX: Update VIP status
    [HttpPost]
    public async Task<IActionResult> UpdateVipStatus(int contactId, bool isVip)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var contact = await _context.Contacts
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == contactId && c.Event.UserId == userId);

        if (contact == null)
        {
            return NotFound();
        }

        contact.IsVip = isVip;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    private bool ContactExists(int id)
    {
        return _context.Contacts.Any(e => e.Id == id);
    }
}
