using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WeddingInvitationManager.Data;
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

            if (model.ContactsFile != null)
            {
                var contacts = await _contactImportService.ImportFromFileAsync(
                    model.ContactsFile, 
                    model.EventId, 
                    model.Format);

                TempData["Success"] = $"Successfully imported {contacts.Count} contacts!";
            }
            else
            {
                TempData["Error"] = "Please select a file to import.";
            }

            return RedirectToAction("Details", "Events", new { id = model.EventId });
        }
        catch (Exception ex)
        {
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
}
