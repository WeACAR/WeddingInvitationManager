namespace WeddingInvitationManager.Models.ViewModels
{
    public class QRScannerViewModel
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public int TotalInvitations { get; set; }
        public int ScannedCount { get; set; }
        public int ValidScans { get; set; }
        public int InvalidScans { get; set; }
        public bool IsActive { get; set; }
    }

    public class QRScanRequest
    {
        public string QRCode { get; set; } = string.Empty;
        public string GuardName { get; set; } = string.Empty;
        public int EventId { get; set; }
    }

    public class QRScanResponse
    {
        public ScanResult Result { get; set; }
        public string Message { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsVip { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime ScannedAt { get; set; }
        public string GuardName { get; set; } = string.Empty;
        public int RemainingUses { get; set; }
        public DateTime? PreviouslyUsedAt { get; set; }
        public string? PreviouslyUsedBy { get; set; }
    }

    public class QRScanStatsViewModel
    {
        public int TotalScans { get; set; }
        public int ValidScans { get; set; }
        public int InvalidScans { get; set; }
        public int AlreadyUsedScans { get; set; }
        public int ExpiredScans { get; set; }
        public double ScansPerMinute { get; set; }
        public int TotalInvitations { get; set; }
        public int ScannedCount { get; set; }
        public List<RecentScanViewModel> RecentScans { get; set; } = new();
    }

    public class RecentScanViewModel
    {
        public string GuestName { get; set; } = string.Empty;
        public ScanResult Result { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime ScannedAt { get; set; }
        public string GuardName { get; set; } = string.Empty;
        public bool IsVip { get; set; }
    }
}
