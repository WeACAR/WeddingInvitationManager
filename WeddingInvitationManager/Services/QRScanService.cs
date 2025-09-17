using Microsoft.EntityFrameworkCore;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using WeddingInvitationManager.Models.ViewModels;
using System.Collections.Concurrent;

namespace WeddingInvitationManager.Services;

public interface IQRScanService
{
    Task<QRScanResponse> ProcessQRScanAsync(string qrCode, string guardName, int eventId, string? ipAddress = null);
    Task<QRScanStatsViewModel> GetScanStatsAsync(int eventId);
    Task<List<QRScan>> GetRecentScansAsync(int eventId, int count = 50);
}

public class QRScanService : IQRScanService
{
    private readonly ApplicationDbContext _context;
    private readonly ConcurrentDictionary<string, DateTime> _scanningLocks = new();

    public QRScanService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<QRScanResponse> ProcessQRScanAsync(string qrCode, string guardName, int eventId, string? ipAddress = null)
    {
        // Prevent duplicate scans within 2 seconds
        var lockKey = $"{qrCode}_{guardName}";
        if (_scanningLocks.TryGetValue(lockKey, out var lastScan) && 
            DateTime.UtcNow.Subtract(lastScan).TotalSeconds < 2)
        {
            return new QRScanResponse
            {
                Result = ScanResult.Invalid,
                Message = "Please wait before scanning again"
            };
        }

        _scanningLocks[lockKey] = DateTime.UtcNow;

        try
        {
            // First try to find in regular invitations table
            var invitation = await _context.Invitations
                .Include(i => i.Contact)
                .Include(i => i.Event)
                .FirstOrDefaultAsync(i => i.QRCode == qrCode && i.EventId == eventId);

            // If not found, check anonymous invitations table
            AnonymousInvitation? anonymousInvitation = null;
            if (invitation == null)
            {
                anonymousInvitation = await _context.AnonymousInvitations
                    .Include(a => a.Event)
                    .FirstOrDefaultAsync(a => a.QRCode == qrCode && a.EventId == eventId);
            }

            var scanResult = new QRScanResponse
            {
                GuardName = guardName, // Set the guard name for all responses
                ScannedAt = DateTime.UtcNow // Set the scan time
            };

            if (invitation == null && anonymousInvitation == null)
            {
                scanResult.Result = ScanResult.NotFound;
                scanResult.Message = "Invalid QR code or not for this event";
            }
            else if (invitation != null)
            {
                // Process regular invitation
                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    scanResult.Result = ScanResult.Expired;
                    scanResult.Message = "This invitation has expired";
                    scanResult.GuestName = invitation.Contact.Name;
                    scanResult.Category = invitation.Contact.Category ?? "";
                    scanResult.IsVip = invitation.Contact.IsVip;
                }
                else if (invitation.IsUsed)
                {
                    scanResult.Result = ScanResult.AlreadyUsed;
                    scanResult.Message = "This invitation has already been used";
                    scanResult.GuestName = invitation.Contact.Name;
                    scanResult.Category = invitation.Contact.Category ?? "";
                    scanResult.IsVip = invitation.Contact.IsVip;
                    scanResult.PreviouslyUsedAt = invitation.UsedAt;
                    scanResult.PreviouslyUsedBy = invitation.UsedByGuard;
                }
                else
                {
                    // Valid scan - mark as used
                    invitation.IsUsed = true;
                    invitation.UsedAt = DateTime.UtcNow;
                    invitation.UsedByGuard = guardName;

                    scanResult.Result = ScanResult.Valid;
                    scanResult.Message = "Welcome! Valid invitation";
                    scanResult.GuestName = invitation.Contact.Name;
                    scanResult.Category = invitation.Contact.Category ?? "";
                    scanResult.IsVip = invitation.Contact.IsVip;

                    await _context.SaveChangesAsync();
                }
            }
            else if (anonymousInvitation != null)
            {
                // Process anonymous invitation
                if (anonymousInvitation.ExpiresAt < DateTime.UtcNow)
                {
                    scanResult.Result = ScanResult.Expired;
                    scanResult.Message = "This invitation has expired";
                    scanResult.GuestName = anonymousInvitation.GuestName;
                    scanResult.Category = "Anonymous Guest";
                    scanResult.IsVip = false;
                }
                else if (anonymousInvitation.IsUsed)
                {
                    scanResult.Result = ScanResult.AlreadyUsed;
                    scanResult.Message = "This invitation has already been used";
                    scanResult.GuestName = anonymousInvitation.GuestName;
                    scanResult.Category = "Anonymous Guest";
                    scanResult.IsVip = false;
                    scanResult.PreviouslyUsedAt = anonymousInvitation.UsedAt;
                    scanResult.PreviouslyUsedBy = guardName; // We don't track who used anonymous invitations
                }
                else
                {
                    // Valid scan - mark as used
                    anonymousInvitation.IsUsed = true;
                    anonymousInvitation.UsedAt = DateTime.UtcNow;

                    scanResult.Result = ScanResult.Valid;
                    scanResult.Message = "Welcome! Valid anonymous invitation";
                    scanResult.GuestName = anonymousInvitation.GuestName;
                    scanResult.Category = "Anonymous Guest";
                    scanResult.IsVip = false;

                    await _context.SaveChangesAsync();
                }
            }

            // Log the scan
            var qrScan = new QRScan
            {
                ScannedBy = guardName,
                ScannedAt = DateTime.UtcNow,
                Result = scanResult.Result,
                Notes = scanResult.Message,
                IpAddress = ipAddress,
                EventId = eventId
            };

            // Set the appropriate foreign key and guest information based on invitation type
            if (invitation != null)
            {
                qrScan.InvitationId = invitation.Id;
                qrScan.GuestName = invitation.Contact?.Name ?? "Unknown";
                qrScan.Category = invitation.Contact?.Category ?? "Unknown";
                qrScan.IsVip = invitation.Contact?.IsVip ?? false;
            }
            else if (anonymousInvitation != null)
            {
                qrScan.AnonymousInvitationId = anonymousInvitation.Id;
                qrScan.GuestName = anonymousInvitation.GuestName;
                qrScan.Category = "Anonymous";
                qrScan.IsVip = false;
            }

            _context.QRScans.Add(qrScan);
            await _context.SaveChangesAsync();

            return scanResult;
        }
        finally
        {
            // Clean up old locks (older than 10 seconds)
            var cutoff = DateTime.UtcNow.AddSeconds(-10);
            var keysToRemove = _scanningLocks
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _scanningLocks.TryRemove(key, out _);
            }
        }
    }

    public async Task<QRScanStatsViewModel> GetScanStatsAsync(int eventId)
    {
        // Count regular invitations
        var totalInvitations = await _context.Invitations.CountAsync(i => i.EventId == eventId);
        var scannedCount = await _context.Invitations.CountAsync(i => i.EventId == eventId && i.IsUsed);
        
        // Count anonymous invitations
        var totalAnonymousInvitations = await _context.AnonymousInvitations.CountAsync(a => a.EventId == eventId);
        var scannedAnonymousCount = await _context.AnonymousInvitations.CountAsync(a => a.EventId == eventId && a.IsUsed);
        
        var scanCounts = await _context.QRScans
            .Where(q => q.EventId == eventId)
            .GroupBy(q => q.Result)
            .Select(g => new { Result = g.Key, Count = g.Count() })
            .ToListAsync();

        return new QRScanStatsViewModel
        {
            TotalInvitations = totalInvitations + totalAnonymousInvitations,
            ScannedCount = scannedCount + scannedAnonymousCount,
            ValidScans = scanCounts.FirstOrDefault(s => s.Result == ScanResult.Valid)?.Count ?? 0,
            InvalidScans = scanCounts.FirstOrDefault(s => s.Result == ScanResult.Invalid)?.Count ?? 0,
            AlreadyUsedScans = scanCounts.FirstOrDefault(s => s.Result == ScanResult.AlreadyUsed)?.Count ?? 0,
            ExpiredScans = scanCounts.FirstOrDefault(s => s.Result == ScanResult.Expired)?.Count ?? 0
        };
    }

    public async Task<List<QRScan>> GetRecentScansAsync(int eventId, int count = 50)
    {
        return await _context.QRScans
            .Include(q => q.Invitation)
                .ThenInclude(i => i!.Contact)
            .Include(q => q.AnonymousInvitation)
            .Where(q => q.EventId == eventId)
            .OrderByDescending(q => q.ScannedAt)
            .Take(count)
            .ToListAsync();
    }
}
