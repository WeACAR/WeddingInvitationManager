using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models.ViewModels;
using WeddingInvitationManager.Services;
using WeddingInvitationManager.Hubs;

namespace WeddingInvitationManager.Controllers;

[Authorize]
public class QRScannerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IQRScanService _qrScanService;
    private readonly HighPerformanceQRScanService _highPerfScanService;
    private readonly IHubContext<QRScanHub> _hubContext;

    public QRScannerController(
        ApplicationDbContext context, 
        IQRScanService qrScanService,
        HighPerformanceQRScanService highPerfScanService,
        IHubContext<QRScanHub> hubContext)
    {
        _context = context;
        _qrScanService = qrScanService;
        _highPerfScanService = highPerfScanService;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var events = await _context.Events
            .Where(e => e.UserId == userId && e.Date >= DateTime.Today)
            .OrderBy(e => e.Date)
            .ToListAsync();

        return View(events);
    }

    public async Task<IActionResult> Scanner(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        var model = new QRScannerViewModel
        {
            EventId = eventId,
            EventName = eventEntity.Name
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessScan([FromBody] QRScanRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var eventEntity = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == request.EventId && e.UserId == userId);

            if (eventEntity == null)
                return Json(new { success = false, message = "Event not found" });

            var result = await _qrScanService.ProcessQRScanAsync(
                request.QRCode, 
                request.GuardName, 
                request.EventId,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            // Notify all connected clients about the scan
            await _hubContext.NotifyQRScanAsync(request.EventId, new
            {
                result.Result,
                result.Message,
                result.GuestName,
                result.Category,
                result.IsVip,
                ScanTime = DateTime.UtcNow,
                GuardName = request.GuardName
            });

            // Update stats
            var stats = await _qrScanService.GetScanStatsAsync(request.EventId);
            await _hubContext.NotifyStatsUpdateAsync(request.EventId, stats);

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> Stats(int eventId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);

        if (eventEntity == null)
            return NotFound();

        var stats = await _qrScanService.GetScanStatsAsync(eventId);
        var recentScans = await _qrScanService.GetRecentScansAsync(eventId, 20);

        ViewBag.Event = eventEntity;
        ViewBag.RecentScans = recentScans;
        
        return View(stats);
    }

    [HttpGet]
    public async Task<IActionResult> GetStats(int eventId)
    {
        try
        {
            var stats = await _qrScanService.GetScanStatsAsync(eventId);
            return Json(stats);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentScans(int eventId, int count = 10)
    {
        try
        {
            var scans = await _highPerfScanService.GetRecentScansAsync(eventId, count);
            return Json(new { success = true, data = scans });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // High-performance scan processing endpoint
    [HttpPost]
    public async Task<IActionResult> ProcessScan([FromBody] ProcessScanRequest request)
    {
        try
        {
            var result = await _highPerfScanService.ProcessScanAsync(
                request.QRCode, 
                request.GuardName, 
                request.EventId);

            // Broadcast to SignalR clients for real-time updates
            await _hubContext.Clients.Group($"Event_{request.EventId}")
                .SendAsync("ScanResult", new
                {
                    QRCode = request.QRCode,
                    GuestName = result.GuestName,
                    Result = result.Result.ToString(),
                    IsVip = result.IsVip,
                    Category = result.Category,
                    ScannedAt = result.ScannedAt,
                    GuardName = request.GuardName
                });

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Get event statistics
    [HttpGet]
    public async Task<IActionResult> GetEventStats(int eventId)
    {
        try
        {
            var stats = await _highPerfScanService.GetEventStatisticsAsync(eventId);
            return Json(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Public endpoint for door guards (no authorization required)
    [AllowAnonymous]
    public async Task<IActionResult> Guard(int eventId, string? key = null)
    {
        // You can add a simple key-based authentication for guards
        // Or use a different authentication method
        
        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
            return NotFound();

        var model = new QRScannerViewModel
        {
            EventId = eventId,
            EventName = eventEntity.Name
        };

        return View("GuardScanner", model);
    }
}

public class QRScanRequest
{
    public string QRCode { get; set; } = string.Empty;
    public string GuardName { get; set; } = string.Empty;
    public int EventId { get; set; }
}
