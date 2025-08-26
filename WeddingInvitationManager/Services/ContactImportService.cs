using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WeddingInvitationManager.Data;
using WeddingInvitationManager.Models;
using WeddingInvitationManager.Models.ViewModels;

namespace WeddingInvitationManager.Services;

public interface IContactImportService
{
    Task<List<Contact>> ImportFromFileAsync(IFormFile file, int eventId, ContactFileFormat format);
    Task<List<Contact>> ImportFromManualEntryAsync(List<ContactRowViewModel> contacts, int eventId);
    Task<byte[]> ExportContactsAsync(int eventId, string format = "csv");
    Task<string> GenerateContactTemplateAsync(string format = "csv");
}

public class ContactImportService : IContactImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ContactImportService(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<List<Contact>> ImportFromFileAsync(IFormFile file, int eventId, ContactFileFormat format)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty or null");

        var contacts = new List<Contact>();

        try
        {
            switch (format)
            {
                case ContactFileFormat.CSV:
                    contacts = await ImportFromCsvAsync(file, eventId);
                    break;
                case ContactFileFormat.Excel:
                    contacts = await ImportFromExcelAsync(file, eventId);
                    break;
                case ContactFileFormat.VCard:
                    contacts = await ImportFromVCardAsync(file, eventId);
                    break;
                default:
                    throw new ArgumentException("Unsupported file format");
            }

            // Validate and save contacts
            var validContacts = new List<Contact>();
            foreach (var contact in contacts)
            {
                if (await IsValidContactAsync(contact, eventId))
                {
                    validContacts.Add(contact);
                }
            }

            if (validContacts.Any())
            {
                _context.Contacts.AddRange(validContacts);
                await _context.SaveChangesAsync();
            }

            return validContacts;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error importing contacts: {ex.Message}", ex);
        }
    }

    private async Task<List<Contact>> ImportFromCsvAsync(IFormFile file, int eventId)
    {
        var contacts = new List<Contact>();
        
        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        var records = csv.GetRecords<ContactCsvModel>().ToList();
        
        foreach (var record in records)
        {
            contacts.Add(new Contact
            {
                Name = record.Name?.Trim() ?? "",
                PhoneNumber = CleanPhoneNumber(record.PhoneNumber ?? ""),
                Email = record.Email?.Trim(),
                Category = record.Category?.Trim(),
                IsVip = record.IsVip.HasValue && record.IsVip.Value,
                EventId = eventId
            });
        }

        return contacts;
    }

    private async Task<List<Contact>> ImportFromExcelAsync(IFormFile file, int eventId)
    {
        var contacts = new List<Contact>();
        
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        
        var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header
        
        foreach (var row in rows)
        {
            var name = row.Cell(1).GetString().Trim();
            var phone = CleanPhoneNumber(row.Cell(2).GetString());
            var email = row.Cell(3).GetString().Trim();
            var category = row.Cell(4).GetString().Trim();
            var isVip = row.Cell(5).GetString().ToLower() == "true" || row.Cell(5).GetString() == "1";

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(phone))
            {
                contacts.Add(new Contact
                {
                    Name = name,
                    PhoneNumber = phone,
                    Email = string.IsNullOrEmpty(email) ? null : email,
                    Category = string.IsNullOrEmpty(category) ? null : category,
                    IsVip = isVip,
                    EventId = eventId
                });
            }
        }

        return contacts;
    }

    private async Task<List<Contact>> ImportFromVCardAsync(IFormFile file, int eventId)
    {
        // Basic vCard parsing - you might want to use a dedicated library
        var contacts = new List<Contact>();
        
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        
        var vCards = content.Split("BEGIN:VCARD", StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var vCard in vCards)
        {
            if (!vCard.Contains("END:VCARD")) continue;
            
            var lines = vCard.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var contact = new Contact { EventId = eventId };
            
            foreach (var line in lines)
            {
                if (line.StartsWith("FN:"))
                    contact.Name = line.Substring(3).Trim();
                else if (line.StartsWith("TEL:"))
                    contact.PhoneNumber = CleanPhoneNumber(line.Substring(4));
                else if (line.StartsWith("EMAIL:"))
                    contact.Email = line.Substring(6).Trim();
            }
            
            if (!string.IsNullOrEmpty(contact.Name) && !string.IsNullOrEmpty(contact.PhoneNumber))
            {
                contacts.Add(contact);
            }
        }

        return contacts;
    }

    public async Task<List<Contact>> ImportFromManualEntryAsync(List<ContactRowViewModel> contacts, int eventId)
    {
        var validContacts = new List<Contact>();
        
        foreach (var contactVm in contacts)
        {
            var contact = new Contact
            {
                Name = contactVm.Name.Trim(),
                PhoneNumber = CleanPhoneNumber(contactVm.PhoneNumber),
                Email = string.IsNullOrEmpty(contactVm.Email) ? null : contactVm.Email.Trim(),
                Category = string.IsNullOrEmpty(contactVm.Category) ? null : contactVm.Category.Trim(),
                IsVip = contactVm.IsVip,
                EventId = eventId
            };

            if (await IsValidContactAsync(contact, eventId))
            {
                validContacts.Add(contact);
            }
        }

        if (validContacts.Any())
        {
            _context.Contacts.AddRange(validContacts);
            await _context.SaveChangesAsync();
        }

        return validContacts;
    }

    public async Task<byte[]> ExportContactsAsync(int eventId, string format = "csv")
    {
        var contacts = await _context.Contacts
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (format.ToLower() == "excel")
        {
            return ExportToExcel(contacts);
        }
        else
        {
            return ExportToCsv(contacts);
        }
    }

    public async Task<string> GenerateContactTemplateAsync(string format = "csv")
    {
        var fileName = $"contact_template_{DateTime.UtcNow:yyyyMMdd}.{format}";
        var filePath = Path.Combine(_environment.WebRootPath, "uploads", "temp", fileName);

        if (format.ToLower() == "excel")
        {
            CreateExcelTemplate(filePath);
        }
        else
        {
            CreateCsvTemplate(filePath);
        }

        return $"/uploads/temp/{fileName}";
    }

    private string CleanPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        
        // Remove all non-digit characters except +
        var cleaned = string.Concat(phone.Where(c => char.IsDigit(c) || c == '+'));
        
        // Ensure it starts with + if it's an international number
        if (!cleaned.StartsWith("+") && cleaned.Length > 10)
        {
            cleaned = "+" + cleaned;
        }
        
        return cleaned;
    }

    private async Task<bool> IsValidContactAsync(Contact contact, int eventId)
    {
        if (string.IsNullOrEmpty(contact.Name) || string.IsNullOrEmpty(contact.PhoneNumber))
            return false;

        // Check for duplicate phone numbers in the same event
        var exists = await _context.Contacts
            .AnyAsync(c => c.EventId == eventId && c.PhoneNumber == contact.PhoneNumber);

        return !exists;
    }

    private byte[] ExportToCsv(List<Contact> contacts)
    {
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        csv.WriteField("Name");
        csv.WriteField("PhoneNumber");
        csv.WriteField("Email");
        csv.WriteField("Category");
        csv.WriteField("IsVip");
        csv.NextRecord();

        foreach (var contact in contacts)
        {
            csv.WriteField(contact.Name);
            csv.WriteField(contact.PhoneNumber);
            csv.WriteField(contact.Email ?? "");
            csv.WriteField(contact.Category ?? "");
            csv.WriteField(contact.IsVip ? "Yes" : "No");
            csv.NextRecord();
        }

        return System.Text.Encoding.UTF8.GetBytes(writer.ToString());
    }

    private byte[] ExportToExcel(List<Contact> contacts)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Contacts");

        // Headers
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "Phone Number";
        worksheet.Cell(1, 3).Value = "Email";
        worksheet.Cell(1, 4).Value = "Category";
        worksheet.Cell(1, 5).Value = "VIP";

        // Data
        for (int i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            worksheet.Cell(i + 2, 1).Value = contact.Name;
            worksheet.Cell(i + 2, 2).Value = contact.PhoneNumber;
            worksheet.Cell(i + 2, 3).Value = contact.Email ?? "";
            worksheet.Cell(i + 2, 4).Value = contact.Category ?? "";
            worksheet.Cell(i + 2, 5).Value = contact.IsVip ? "Yes" : "No";
        }

        worksheet.ColumnsUsed().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void CreateCsvTemplate(string filePath)
    {
        var template = "Name,PhoneNumber,Email,Category,IsVip\n";
        template += "John Doe,+1234567890,john@example.com,Family,No\n";
        template += "Jane Smith,+1234567891,jane@example.com,Friends,Yes\n";
        
        File.WriteAllText(filePath, template);
    }

    private void CreateExcelTemplate(string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Contacts");

        // Headers
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "PhoneNumber";
        worksheet.Cell(1, 3).Value = "Email";
        worksheet.Cell(1, 4).Value = "Category";
        worksheet.Cell(1, 5).Value = "IsVip";

        // Sample data
        worksheet.Cell(2, 1).Value = "John Doe";
        worksheet.Cell(2, 2).Value = "+1234567890";
        worksheet.Cell(2, 3).Value = "john@example.com";
        worksheet.Cell(2, 4).Value = "Family";
        worksheet.Cell(2, 5).Value = "No";

        worksheet.Cell(3, 1).Value = "Jane Smith";
        worksheet.Cell(3, 2).Value = "+1234567891";
        worksheet.Cell(3, 3).Value = "jane@example.com";
        worksheet.Cell(3, 4).Value = "Friends";
        worksheet.Cell(3, 5).Value = "Yes";

        worksheet.ColumnsUsed().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}

public class ContactCsvModel
{
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Category { get; set; }
    public bool? IsVip { get; set; }
}
