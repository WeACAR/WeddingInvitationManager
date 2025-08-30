using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using WeddingInvitationManager.Data;

namespace WeddingInvitationManager.Services;

public interface IQRCodeService
{
    string GenerateUniqueQRCode();
    byte[] GenerateQRCodeImage(string data, int size = 200);
    Image GenerateQRCodeForInvitation(string data, int size = 200);
    Task<string> CreateInvitationImageAsync(string templatePath, string qrData, int qrX, int qrY, int qrSize);
    
    // New methods for improved invitation generation
    Task<string> CreateGeneralInvitationAsync(string templatePath, string eventUrl, int qrX, int qrY, int qrSize);
    Task<List<string>> CreateIndividualInvitationsAsync(int eventId, int? templateId = null);
    Task<string> CreatePreviewWithSampleQRAsync(string templatePath, int qrX, int qrY, int qrSize);
    Task<byte[]> CreateInvitationZipAsync(List<string> imagePaths, string eventName);
}

public class QRCodeService : IQRCodeService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _context;

    public QRCodeService(IWebHostEnvironment environment, ApplicationDbContext context)
    {
        _environment = environment;
        _context = context;
    }

    public string GenerateUniqueQRCode()
    {
        // Generate a unique identifier using timestamp + random bytes
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        var combined = BitConverter.GetBytes(timestamp).Concat(randomBytes).ToArray();
        return Convert.ToBase64String(combined).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    public byte[] GenerateQRCodeImage(string data, int size = 200)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(size / 25); // Adjust pixel per module
    }

    public Image GenerateQRCodeForInvitation(string data, int size = 200)
    {
        var qrBytes = GenerateQRCodeImage(data, size);
        return Image.Load(qrBytes);
    }

    public async Task<string> CreateInvitationImageAsync(string templatePath, string qrData, int qrX, int qrY, int qrSize)
    {
        var templateFullPath = Path.Combine(_environment.WebRootPath, templatePath.TrimStart('/'));
        
        if (!File.Exists(templateFullPath))
            throw new FileNotFoundException($"Template image not found: {templatePath}");

        // Generate unique filename
        var fileName = $"{Guid.NewGuid()}.png";
        var outputDir = Path.Combine(_environment.WebRootPath, "generated", "invitations");
        
        // Ensure directory exists
        Directory.CreateDirectory(outputDir);
        
        var outputPath = Path.Combine(outputDir, fileName);

        using var templateImage = await Image.LoadAsync(templateFullPath);
        using var qrImage = GenerateQRCodeForInvitation(qrData, qrSize);

        // Resize QR code if needed
        if (qrImage.Width != qrSize || qrImage.Height != qrSize)
        {
            qrImage.Mutate(x => x.Resize(qrSize, qrSize));
        }

        // Overlay QR code on template
        templateImage.Mutate(ctx =>
        {
            ctx.DrawImage(qrImage, new Point(qrX, qrY), 1f);
        });

        await templateImage.SaveAsPngAsync(outputPath);
        
        return $"/generated/invitations/{fileName}";
    }

    // Create a general invitation with event URL QR code
    public async Task<string> CreateGeneralInvitationAsync(string templatePath, string eventUrl, int qrX, int qrY, int qrSize)
    {
        var templateFullPath = Path.Combine(_environment.WebRootPath, templatePath.TrimStart('/'));
        
        if (!File.Exists(templateFullPath))
            throw new FileNotFoundException($"Template image not found: {templatePath}");

        // Generate filename
        var fileName = $"general_{Guid.NewGuid()}.png";
        var outputDir = Path.Combine(_environment.WebRootPath, "generated", "invitations");
        
        // Ensure directory exists
        Directory.CreateDirectory(outputDir);
        
        var outputPath = Path.Combine(outputDir, fileName);

        using var templateImage = await Image.LoadAsync(templateFullPath);
        using var qrImage = GenerateQRCodeForInvitation(eventUrl, qrSize);

        // Resize QR code if needed
        if (qrImage.Width != qrSize || qrImage.Height != qrSize)
        {
            qrImage.Mutate(x => x.Resize(qrSize, qrSize));
        }

        // Overlay QR code on template
        templateImage.Mutate(ctx =>
        {
            ctx.DrawImage(qrImage, new Point(qrX, qrY), 1f);
        });

        await templateImage.SaveAsPngAsync(outputPath);
        
        return $"/generated/invitations/{fileName}";
    }

    // Create individual invitations for all contacts in an event
    public async Task<List<string>> CreateIndividualInvitationsAsync(int eventId, int? templateId = null)
    {
        var invitationPaths = new List<string>();

        // Get event and contacts
        var eventEntity = await _context.Events
            .Include(e => e.Contacts)
            .Include(e => e.Templates)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
            throw new ArgumentException("Event not found");

        // Get template
        var template = templateId.HasValue 
            ? await _context.InvitationTemplates.FindAsync(templateId.Value)
            : eventEntity.Templates.FirstOrDefault(t => t.IsDefault) ?? eventEntity.Templates.FirstOrDefault();

        if (template == null)
            throw new ArgumentException("No template found");

        // Process each contact
        foreach (var contact in eventEntity.Contacts)
        {
            try
            {
                // Get or create invitation for this contact
                var invitation = await _context.Invitations
                    .FirstOrDefaultAsync(i => i.ContactId == contact.Id && i.EventId == eventId);

                if (invitation == null)
                {
                    // Create new invitation
                    var qrCode = GenerateUniqueQRCode();
                    invitation = new Models.Invitation
                    {
                        QRCode = qrCode,
                        ContactId = contact.Id,
                        EventId = eventId,
                        ExpiresAt = eventEntity.Date.AddDays(1),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Invitations.Add(invitation);
                    await _context.SaveChangesAsync();
                }

                // Select appropriate template (VIP or regular)
                var selectedTemplate = contact.IsVip && eventEntity.Templates.Any(t => t.IsVipTemplate)
                    ? eventEntity.Templates.First(t => t.IsVipTemplate)
                    : template;

                // Generate invitation image
                var imagePath = await CreateInvitationImageAsync(
                    selectedTemplate.ImagePath,
                    invitation.QRCode,
                    selectedTemplate.QRPositionX,
                    selectedTemplate.QRPositionY,
                    selectedTemplate.QRSize);

                // Update invitation with image path
                invitation.ImagePath = imagePath;
                await _context.SaveChangesAsync();

                invitationPaths.Add(imagePath);
            }
            catch (Exception ex)
            {
                // Log error but continue with other contacts
                Console.WriteLine($"Error creating invitation for {contact.Name}: {ex.Message}");
            }
        }

        return invitationPaths;
    }

    // Create template preview with sample QR code
    public async Task<string> CreatePreviewWithSampleQRAsync(string templatePath, int qrX, int qrY, int qrSize)
    {
        var templateFullPath = Path.Combine(_environment.WebRootPath, templatePath.TrimStart('/'));
        
        if (!File.Exists(templateFullPath))
            throw new FileNotFoundException($"Template image not found: {templatePath}");

        // Generate sample QR code data
        var sampleData = "https://example.com/event/sample-invitation";
        
        // Generate filename
        var fileName = $"preview_{Guid.NewGuid()}.png";
        var outputPath = Path.Combine(_environment.WebRootPath, "generated", "invitations", fileName);

        // Ensure directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        using var templateImage = await Image.LoadAsync(templateFullPath);
        using var qrImage = GenerateQRCodeForInvitation(sampleData, qrSize);

        // Resize QR code if needed
        if (qrImage.Width != qrSize || qrImage.Height != qrSize)
        {
            qrImage.Mutate(x => x.Resize(qrSize, qrSize));
        }

        // Overlay QR code on template
        templateImage.Mutate(ctx =>
        {
            ctx.DrawImage(qrImage, new Point(qrX, qrY), 1f);
        });

        await templateImage.SaveAsPngAsync(outputPath);
        
        return $"/generated/invitations/{fileName}";
    }

    // Create ZIP file containing invitation images
    public async Task<byte[]> CreateInvitationZipAsync(List<string> imagePaths, string eventName)
    {
        if (imagePaths == null || !imagePaths.Any())
            throw new ArgumentException("No image paths provided for ZIP creation");

        var zipFileName = $"{eventName}_Invitations_{DateTime.UtcNow:yyyyMMdd}.zip";
        var tempZipPath = Path.Combine(Path.GetTempPath(), zipFileName);

        using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
        {
            int addedFiles = 0;
            for (int i = 0; i < imagePaths.Count; i++)
            {
                var imagePath = imagePaths[i];
                var fullImagePath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                
                if (File.Exists(fullImagePath))
                {
                    var entryName = $"invitation_{i + 1:D3}.png";
                    archive.CreateEntryFromFile(fullImagePath, entryName);
                    addedFiles++;
                }
                else
                {
                    Console.WriteLine($"Warning: Image file not found: {fullImagePath}");
                }
            }
            
            if (addedFiles == 0)
            {
                throw new InvalidOperationException("No valid image files found to add to ZIP");
            }
        }

        var zipBytes = await File.ReadAllBytesAsync(tempZipPath);
        File.Delete(tempZipPath); // Clean up temp file
        
        return zipBytes;
    }
}
