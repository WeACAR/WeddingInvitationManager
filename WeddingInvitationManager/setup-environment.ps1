# Wedding Invitation Manager - Environment Variables Setup Script
# Run this script to set up environment variables for your application

Write-Host "Setting up Environment Variables for Wedding Invitation Manager..." -ForegroundColor Green

# Database Connection
Write-Host "Setting up Database Configuration..." -ForegroundColor Yellow
[System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=db.dtcjjrshssmciwkrnxwk.supabase.co;Database=postgres;Username=postgres;Password=yvomRQN7|d87;SSL Mode=Require", "User")

# Logging Configuration
Write-Host "Setting up Logging Configuration..." -ForegroundColor Yellow
[System.Environment]::SetEnvironmentVariable("Logging__LogLevel__Default", "Information", "User")
[System.Environment]::SetEnvironmentVariable("Logging__LogLevel__Microsoft.AspNetCore", "Warning", "User")

# General Configuration
Write-Host "Setting up General Configuration..." -ForegroundColor Yellow
[System.Environment]::SetEnvironmentVariable("AllowedHosts", "*", "User")

# WhatsApp Configuration
Write-Host "Setting up WhatsApp Configuration..." -ForegroundColor Yellow
[System.Environment]::SetEnvironmentVariable("WhatsApp__ApiUrl", "https://api.whatsapp.com/send", "User")
[System.Environment]::SetEnvironmentVariable("WhatsApp__ApiKey", "", "User")
[System.Environment]::SetEnvironmentVariable("WhatsApp__BusinessNumber", "", "User")

# File Upload Configuration
Write-Host "Setting up File Upload Configuration..." -ForegroundColor Yellow
[System.Environment]::SetEnvironmentVariable("FileUpload__MaxSizeBytes", "10485760", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedImageTypes__0", ".jpg", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedImageTypes__1", ".jpeg", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedImageTypes__2", ".png", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedImageTypes__3", ".gif", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedImageTypes__4", ".bmp", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedContactTypes__0", ".csv", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedContactTypes__1", ".xlsx", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedContactTypes__2", ".xls", "User")
[System.Environment]::SetEnvironmentVariable("FileUpload__AllowedContactTypes__3", ".vcf", "User")

Write-Host "Environment variables have been set successfully!" -ForegroundColor Green
Write-Host "You may need to restart your development environment for changes to take effect." -ForegroundColor Cyan

# Display current environment variables for verification
Write-Host "`nVerifying Environment Variables:" -ForegroundColor Magenta
Write-Host "ConnectionStrings__DefaultConnection: $([System.Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'User'))"
Write-Host "WhatsApp__ApiUrl: $([System.Environment]::GetEnvironmentVariable('WhatsApp__ApiUrl', 'User'))"
Write-Host "FileUpload__MaxSizeBytes: $([System.Environment]::GetEnvironmentVariable('FileUpload__MaxSizeBytes', 'User'))"
