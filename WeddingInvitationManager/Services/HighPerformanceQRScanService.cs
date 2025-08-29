using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using System.Collections.Concurrent;

namespace WeddingInvitationManager.Services
{
    public class HighPerformanceQRScanService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HighPerformanceQRScanService> _logger;
        
        // Concurrent collections for high-performance operations
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _scanSemaphores = new();
        private readonly ConcurrentDictionary<string, QRScanResult> _recentScans = new();
        
        // Cache keys and settings
        private const string INVITATION_CACHE_PREFIX = "invitation_";
        private const int CACHE_DURATION_MINUTES = 30;
        private const int RECENT_SCANS_LIMIT = 1000;

        public HighPerformanceQRScanService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<HighPerformanceQRScanService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<QRScanResult> ProcessScanAsync(string qrCode, string guardName, int eventId)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(qrCode) || string.IsNullOrWhiteSpace(guardName))
            {
                return new QRScanResult 
                { 
                    Success = false, 
                    Result = ScanResultType.Invalid, 
                    Message = "Invalid QR code or guard name" 
                };
            }

            // Get or create semaphore for this QR code to prevent duplicate processing
            var semaphore = _scanSemaphores.GetOrAdd(qrCode, _ => new SemaphoreSlim(1, 1));
            
            try
            {
                // Wait for exclusive access to this QR code (timeout after 5 seconds)
                if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    return new QRScanResult 
                    { 
                        Success = false, 
                        Result = ScanResultType.Processing, 
                        Message = "QR code is being processed by another scanner" 
                    };
                }

                // Check recent scans cache first (in-memory, fastest)
                if (_recentScans.TryGetValue(qrCode, out var recentResult))
                {
                    if (recentResult.ScannedAt > DateTime.UtcNow.AddMinutes(-5)) // Within last 5 minutes
                    {
                        _logger.LogInformation($"QR {qrCode} found in recent scans cache");
                        return recentResult;
                    }
                    else
                    {
                        _recentScans.TryRemove(qrCode, out _); // Remove expired entry
                    }
                }

                // Check memory cache for invitation
                var cacheKey = INVITATION_CACHE_PREFIX + qrCode;
                var invitation = _cache.Get<Invitation>(cacheKey);

                if (invitation == null)
                {
                    // Cache miss - query database with optimized query
                    invitation = await _context.Invitations
                        .AsNoTracking() // Read-only, no change tracking overhead
                        .Include(i => i.Contact)
                        .Include(i => i.Event)
                        .FirstOrDefaultAsync(i => i.QRCode == qrCode);

                    if (invitation != null)
                    {
                        // Cache the invitation for future scans
                        _cache.Set(cacheKey, invitation, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                        _logger.LogInformation($"Cached invitation for QR {qrCode}");
                    }
                }

                // Process the scan result
                var result = await ProcessInvitationScanAsync(invitation, guardName, eventId, qrCode);
                
                // Add to recent scans cache
                if (_recentScans.Count >= RECENT_SCANS_LIMIT)
                {
                    // Remove oldest entries when limit reached
                    var oldestEntries = _recentScans
                        .OrderBy(kvp => kvp.Value.ScannedAt)
                        .Take(100)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    
                    foreach (var oldKey in oldestEntries)
                    {
                        _recentScans.TryRemove(oldKey, out _);
                    }
                }
                
                _recentScans.TryAdd(qrCode, result);
                
                return result;
            }
            finally
            {
                semaphore.Release();
                
                // Clean up semaphore if no longer needed
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    if (semaphore.CurrentCount == 1) // No one waiting
                    {
                        _scanSemaphores.TryRemove(qrCode, out _);
                        semaphore.Dispose();
                    }
                });
            }
        }

        private async Task<QRScanResult> ProcessInvitationScanAsync(Invitation? invitation, string guardName, int eventId, string qrCode)
        {
            var scanTime = DateTime.UtcNow;
            var result = new QRScanResult { ScannedAt = scanTime };

            if (invitation == null)
            {
                result.Success = false;
                result.Result = ScanResultType.NotFound;
                result.Message = "QR code not found";
                
                // Log failed scan attempt without creating database record
                _logger.LogWarning($"Unknown QR code scanned: {qrCode} by guard: {guardName}");
                return result;
            }

            // Validate event match
            if (invitation.EventId != eventId)
            {
                result.Success = false;
                result.Result = ScanResultType.WrongEvent;
                result.Message = "QR code is for a different event";
                result.GuestName = invitation.Contact?.Name;
                return result;
            }

            // Check if already used (optimized query)
            var existingScan = await _context.QRScans
                .AsNoTracking()
                .Where(s => s.InvitationId == invitation.Id)
                .OrderByDescending(s => s.ScannedAt)
                .FirstOrDefaultAsync();

            if (existingScan != null)
            {
                result.Success = false;
                result.Result = ScanResultType.AlreadyUsed;
                result.Message = $"Already used at {existingScan.ScannedAt:HH:mm:ss}";
                result.GuestName = invitation.Contact?.Name;
                result.Category = invitation.Contact?.Category;
                result.IsVip = invitation.Contact?.IsVip ?? false;
                result.PreviouslyUsedAt = existingScan.ScannedAt;
                return result;
            }

            // Check if expired
            if (invitation.ExpiresAt < scanTime)
            {
                result.Success = false;
                result.Result = ScanResultType.Expired;
                result.Message = "Invitation has expired";
                result.GuestName = invitation.Contact?.Name;
                result.Category = invitation.Contact?.Category;
                result.IsVip = invitation.Contact?.IsVip ?? false;
                return result;
            }

            // Valid scan - create scan record asynchronously for better performance
            try
            {
                var scanRecord = new QRScan
                {
                    InvitationId = invitation.Id,
                    ScannedAt = scanTime,
                    ScannedBy = guardName,
                    Result = ScanResult.Valid
                };

                _context.QRScans.Add(scanRecord);
                await _context.SaveChangesAsync();

                // Invalidate cache for this invitation since it's now used
                var cacheKey = INVITATION_CACHE_PREFIX + qrCode;
                _cache.Remove(cacheKey);

                result.Success = true;
                result.Result = ScanResultType.Valid;
                result.Message = "Welcome!";
                result.GuestName = invitation.Contact?.Name;
                result.Category = invitation.Contact?.Category;
                result.IsVip = invitation.Contact?.IsVip ?? false;
                result.ScanId = scanRecord.Id;

                _logger.LogInformation($"Valid scan recorded: {qrCode} for {result.GuestName} by {guardName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to record scan for QR {qrCode}");
                result.Success = false;
                result.Result = ScanResultType.DatabaseError;
                result.Message = "Failed to record scan";
            }

            return result;
        }

        public async Task<List<QRScanResult>> GetRecentScansAsync(int eventId, int count = 10)
        {
            // Get from recent scans cache first, then database
            var recentCacheScans = _recentScans.Values
                .Where(s => s.ScannedAt > DateTime.UtcNow.AddMinutes(-30))
                .OrderByDescending(s => s.ScannedAt)
                .Take(count / 2) // Take half from cache
                .ToList();

            var dbScans = await _context.QRScans
                .AsNoTracking()
                .Include(s => s.Invitation)
                .ThenInclude(i => i.Contact)
                .Where(s => s.Invitation.EventId == eventId)
                .OrderByDescending(s => s.ScannedAt)
                .Take(count)
                .Select(s => new QRScanResult
                {
                    ScanId = s.Id,
                    ScannedAt = s.ScannedAt,
                    GuestName = s.Invitation.Contact!.Name,
                    Category = s.Invitation.Contact.Category,
                    IsVip = s.Invitation.Contact.IsVip,
                    Result = (ScanResultType)(int)s.Result,
                    Success = s.Result == ScanResult.Valid
                })
                .ToListAsync();

            // Combine and deduplicate
            var combined = recentCacheScans.Concat(dbScans)
                .GroupBy(s => s.ScanId)
                .Select(g => g.First())
                .OrderByDescending(s => s.ScannedAt)
                .Take(count)
                .ToList();

            return combined;
        }

        public async Task<Dictionary<string, object>> GetEventStatisticsAsync(int eventId)
        {
            // Use compiled queries for better performance
            var stats = await _context.QRScans
                .AsNoTracking()
                .Where(s => s.Invitation.EventId == eventId)
                .GroupBy(s => s.Result)
                .Select(g => new { Result = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalInvitations = await _context.Invitations
                .AsNoTracking()
                .CountAsync(i => i.EventId == eventId);

            var scannedCount = stats.Sum(s => s.Count);
            var validScans = stats.FirstOrDefault(s => s.Result == ScanResult.Valid)?.Count ?? 0;

            return new Dictionary<string, object>
            {
                ["TotalInvitations"] = totalInvitations,
                ["ScannedCount"] = scannedCount,
                ["ValidScans"] = validScans,
                ["InvalidScans"] = scannedCount - validScans,
                ["SuccessRate"] = totalInvitations > 0 ? (double)validScans / totalInvitations * 100 : 0,
                ["ScansByResult"] = stats.ToDictionary(s => s.Result.ToString(), s => s.Count)
            };
        }
    }

    public class QRScanResult
    {
        public bool Success { get; set; }
        public ScanResultType Result { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? GuestName { get; set; }
        public string? Category { get; set; }
        public bool IsVip { get; set; }
        public DateTime ScannedAt { get; set; }
        public DateTime? PreviouslyUsedAt { get; set; }
        public int? ScanId { get; set; }
    }

    public enum ScanResultType
    {
        Valid,
        AlreadyUsed,
        Expired,
        NotFound,
        WrongEvent,
        Invalid,
        Processing,
        DatabaseError
    }
}
