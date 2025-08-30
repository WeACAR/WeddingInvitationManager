# Environment Variables Configuration Guide

## Overview
This document explains how to configure the Wedding Invitation Manager application using environment variables instead of hardcoded values in appsettings.json.

## Quick Setup

### Option 1: Using PowerShell Script (Recommended)
1. Run the PowerShell script:
   ```powershell
   .\setup-environment.ps1
   ```

### Option 2: Manual Setup
Set the following environment variables manually:

#### Database Configuration
```bash
ConnectionStrings__DefaultConnection=Host=db.dtcjjrshssmciwkrnxwk.supabase.co;Database=postgres;Username=postgres;Password=yvomRQN7|d87;SSL Mode=Require
```

#### Logging Configuration
```bash
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft.AspNetCore=Warning
```

#### General Configuration
```bash
AllowedHosts=*
```

#### WhatsApp Configuration
```bash
WhatsApp__ApiUrl=https://api.whatsapp.com/send
WhatsApp__ApiKey=YOUR_WHATSAPP_API_KEY
WhatsApp__BusinessNumber=YOUR_BUSINESS_NUMBER
```

#### File Upload Configuration
```bash
FileUpload__MaxSizeBytes=10485760
FileUpload__AllowedImageTypes__0=.jpg
FileUpload__AllowedImageTypes__1=.jpeg
FileUpload__AllowedImageTypes__2=.png
FileUpload__AllowedImageTypes__3=.gif
FileUpload__AllowedImageTypes__4=.bmp
FileUpload__AllowedContactTypes__0=.csv
FileUpload__AllowedContactTypes__1=.xlsx
FileUpload__AllowedContactTypes__2=.xls
FileUpload__AllowedContactTypes__3=.vcf
```

## Setting Environment Variables

### Windows (PowerShell)
```powershell
[System.Environment]::SetEnvironmentVariable("VARIABLE_NAME", "VALUE", "User")
```

### Windows (Command Prompt)
```cmd
setx VARIABLE_NAME "VALUE"
```

### Linux/macOS
```bash
export VARIABLE_NAME="VALUE"
```

## .NET Core Configuration Hierarchy

.NET Core follows this configuration precedence (highest to lowest):
1. Environment Variables
2. appsettings.{Environment}.json
3. appsettings.json
4. Default values

## Array Configuration in Environment Variables

For arrays in JSON configuration:
```json
{
  "FileUpload": {
    "AllowedImageTypes": [".jpg", ".jpeg", ".png"]
  }
}
```

Use indexed environment variables:
```bash
FileUpload__AllowedImageTypes__0=.jpg
FileUpload__AllowedImageTypes__1=.jpeg
FileUpload__AllowedImageTypes__2=.png
```

## Security Best Practices

1. **Never commit sensitive data** like API keys or passwords to version control
2. **Use different values** for development, staging, and production environments
3. **Store sensitive variables** in secure key management systems in production
4. **Use the .env.template file** as a reference for required variables

## Development vs Production

### Development
- Use the provided values for testing
- Store in user environment variables
- Override with local .env file if needed

### Production
- Use secure values from your hosting provider
- Store sensitive data in Azure Key Vault, AWS Secrets Manager, etc.
- Never use default passwords in production

## Verification

After setting environment variables, verify they're loaded correctly:

```csharp
// In your application
var connectionString = Configuration.GetConnectionString("DefaultConnection");
var whatsAppApiKey = Configuration["WhatsApp:ApiKey"];
```

## Troubleshooting

1. **Environment variables not loading**: Restart your development environment
2. **Array values not working**: Ensure you're using the correct index format (0, 1, 2, etc.)
3. **Nested values not working**: Use double underscores (__) to separate hierarchy levels

## Example Usage in Code

```csharp
// Program.cs or Startup.cs
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection("WhatsApp"));

builder.Services.Configure<FileUploadOptions>(
    builder.Configuration.GetSection("FileUpload"));
```

## Environment Variable Names Reference

| Configuration Path | Environment Variable |
|-------------------|---------------------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `Logging:LogLevel:Default` | `Logging__LogLevel__Default` |
| `WhatsApp:ApiKey` | `WhatsApp__ApiKey` |
| `FileUpload:MaxSizeBytes` | `FileUpload__MaxSizeBytes` |
| `FileUpload:AllowedImageTypes[0]` | `FileUpload__AllowedImageTypes__0` |
