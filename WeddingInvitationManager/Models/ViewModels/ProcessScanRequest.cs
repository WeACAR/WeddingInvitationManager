namespace WeddingInvitationManager.Models.ViewModels
{
    public class ProcessScanRequest
    {
        public string QRCode { get; set; } = string.Empty;
        public string GuardName { get; set; } = string.Empty;
        public int EventId { get; set; }
    }
}
