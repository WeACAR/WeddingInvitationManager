using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;
using System.Text;

namespace WeddingInvitationManager.Services;

public interface IQRCodeService
{
    string GenerateUniqueQRCode();
    byte[] GenerateQRCodeImage(string data, int size = 200);
    Image GenerateQRCodeForInvitation(string data, int size = 200);
    Task<string> CreateInvitationImageAsync(string templatePath, string qrData, int qrX, int qrY, int qrSize);
}

public class QRCodeService : IQRCodeService
{
    private readonly IWebHostEnvironment _environment;

    public QRCodeService(IWebHostEnvironment environment)
    {
        _environment = environment;
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
        var outputPath = Path.Combine(_environment.WebRootPath, "generated", "invitations", fileName);

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
}
