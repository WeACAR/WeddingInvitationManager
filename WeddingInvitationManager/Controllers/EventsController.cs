using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using WeddingInvitationManager.Models.ViewModels;

namespace WeddingInvitationManager.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;

    public EventsController(ApplicationDbContext context)
    {
        _context = context;
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
