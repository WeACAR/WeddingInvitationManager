using Microsoft.EntityFrameworkCore;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using System.Text.Json;

namespace WeddingInvitationManager.Services;

public interface IWhatsAppService
{
    Task<bool> SendInvitationAsync(string phoneNumber, string imagePath, string message, int invitationId);
    Task<List<WhatsAppSendResult>> SendBulkInvitationsAsync(int eventId, int? templateId = null);
    Task<WhatsAppSendStats> GetSendStatsAsync(int eventId);
}

public class WhatsAppService : IWhatsAppService
{
    private readonly ApplicationDbContext _context;
    private readonly IQRCodeService _qrCodeService;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        ApplicationDbContext context,
        IQRCodeService qrCodeService,
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<WhatsAppService> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendInvitationAsync(string phoneNumber, string imagePath, string message, int invitationId)
    {
        try
        {
            // This is a placeholder implementation
            // You would integrate with WhatsApp Business API, Twilio, or similar service
            
            // For demo purposes, we'll simulate sending
            await Task.Delay(100); // Simulate API call delay
            
            // Update invitation as sent
            var invitation = await _context.Invitations.FindAsync(invitationId);
            if (invitation != null)
            {
                invitation.IsSent = true;
                invitation.SentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"Invitation sent to {phoneNumber}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send invitation to {phoneNumber}");
            return false;
        }
    }

    public async Task<List<WhatsAppSendResult>> SendBulkInvitationsAsync(int eventId, int? templateId = null)
    {
        var results = new List<WhatsAppSendResult>();
        
        // Get event and template
        var eventEntity = await _context.Events.FindAsync(eventId);
        if (eventEntity == null)
            throw new ArgumentException("Event not found");

        var template = templateId.HasValue 
            ? await _context.InvitationTemplates.FindAsync(templateId.Value)
            : await _context.InvitationTemplates
                .Where(t => t.EventId == eventId && t.IsDefault)
                .FirstOrDefaultAsync();

        if (template == null)
            throw new ArgumentException("No template found");

        // Get contacts who don't have invitations yet
        var contacts = await _context.Contacts
            .Where(c => c.EventId == eventId && !c.Invitations.Any())
            .ToListAsync();

        // Process in batches to avoid overwhelming the WhatsApp API
        var batchSize = 10;
        var batches = contacts.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var tasks = batch.Select(async contact =>
            {
                try
                {
                    // Generate unique QR code
                    var qrCode = _qrCodeService.GenerateUniqueQRCode();
                    
                    // Create invitation
                    var invitation = new Invitation
                    {
                        QRCode = qrCode,
                        ContactId = contact.Id,
                        EventId = eventId,
                        ExpiresAt = eventEntity.Date.AddDays(1), // Expires 1 day after event
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Invitations.Add(invitation);
                    await _context.SaveChangesAsync();

                    // Select template based on VIP status
                    var selectedTemplate = contact.IsVip && 
                        await _context.InvitationTemplates.AnyAsync(t => t.EventId == eventId && t.IsVipTemplate)
                        ? await _context.InvitationTemplates.FirstAsync(t => t.EventId == eventId && t.IsVipTemplate)
                        : template;

                    // Generate invitation image
                    var imagePath = await _qrCodeService.CreateInvitationImageAsync(
                        selectedTemplate.ImagePath,
                        qrCode,
                        selectedTemplate.QRPositionX,
                        selectedTemplate.QRPositionY,
                        selectedTemplate.QRSize);

                    invitation.ImagePath = imagePath;

                    // Create message
                    var message = $"You're invited to {eventEntity.Name}!\n" +
                                $"Host: {eventEntity.Host}\n" +
                                $"Date: {eventEntity.Date:MMM dd, yyyy}\n" +
                                (string.IsNullOrEmpty(eventEntity.LocationLink) ? "" : $"Location: {eventEntity.LocationLink}\n") +
                                "Please present this QR code at the entrance.";

                    // Send via WhatsApp
                    var success = await SendInvitationAsync(contact.PhoneNumber, imagePath, message, invitation.Id);

                    await _context.SaveChangesAsync();

                    return new WhatsAppSendResult
                    {
                        ContactName = contact.Name,
                        PhoneNumber = contact.PhoneNumber,
                        Success = success,
                        Message = success ? "Sent successfully" : "Failed to send",
                        InvitationId = invitation.Id
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing invitation for {contact.Name}");
                    return new WhatsAppSendResult
                    {
                        ContactName = contact.Name,
                        PhoneNumber = contact.PhoneNumber,
                        Success = false,
                        Message = ex.Message
                    };
                }
            });

            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);

            // Add delay between batches to respect API rate limits
            if (batches.Count() > 1)
                await Task.Delay(2000);
        }

        return results;
    }

    public async Task<WhatsAppSendStats> GetSendStatsAsync(int eventId)
    {
        var totalContacts = await _context.Contacts.CountAsync(c => c.EventId == eventId);
        var sentInvitations = await _context.Invitations.CountAsync(i => i.EventId == eventId && i.IsSent);
        var pendingInvitations = await _context.Contacts
            .CountAsync(c => c.EventId == eventId && !c.Invitations.Any());

        return new WhatsAppSendStats
        {
            TotalContacts = totalContacts,
            SentInvitations = sentInvitations,
            PendingInvitations = pendingInvitations,
            SendRate = totalContacts > 0 ? (double)sentInvitations / totalContacts * 100 : 0
        };
    }
}

public class WhatsAppSendResult
{
    public string ContactName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? InvitationId { get; set; }
}

public class WhatsAppSendStats
{
    public int TotalContacts { get; set; }
    public int SentInvitations { get; set; }
    public int PendingInvitations { get; set; }
    public double SendRate { get; set; }
}
