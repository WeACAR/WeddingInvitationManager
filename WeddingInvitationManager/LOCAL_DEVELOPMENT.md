# Local Development Setup Guide

## 🔧 Fixed: Database Connection Issue

The error you encountered was because the environment variables weren't being loaded in your PowerShell session. Here are three ways to fix this:

## ✅ **Solution 1: Use the Local Setup Script (Recommended)**

### For Immediate Use:
```powershell
# Run this before starting the application
.\run-local.ps1
dotnet run
```

The `run-local.ps1` script sets all environment variables for your current PowerShell session.

### For Long-term Use:
1. Copy `run-local.ps1.template` to `run-local.ps1`
2. Update the connection string in the copied file
3. Run the script before each development session

## ✅ **Solution 2: Restart PowerShell (Alternative)**

The environment variables were set at user level, but your current PowerShell session didn't pick them up.

```powershell
# Close and reopen PowerShell, then:
dotnet run
```

## ✅ **Solution 3: Manual Environment Variable (One-time)**

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=db.dtcjjrshssmciwkrnxwk.supabase.co;Database=postgres;Username=postgres;Password=yvomRQN7|d87;SSL Mode=Require"
dotnet run
```

## 🔒 **Security Notes**

### What's Secure:
- ✅ **appsettings.json** - No sensitive data (empty connection string)
- ✅ **appsettings.Development.json** - No sensitive data (empty connection string)  
- ✅ **appsettings.Production.json** - No sensitive data (empty connection string)
- ✅ **run-local.ps1** - Added to .gitignore (won't be committed)

### For Production:
- Use environment variables on Render (as configured)
- Connection string stored securely in Render dashboard
- No sensitive data in your repository

## 🚀 **Recommended Workflow**

### Starting Development:
```powershell
# 1. Set environment variables
.\run-local.ps1

# 2. Start the application
dotnet run

# 3. Access at http://localhost:5131
```

### For Production Deployment:
```bash
# 1. Commit your code (no sensitive data included)
git add .
git commit -m "Update application"
git push origin main

# 2. Render automatically deploys using environment variables
```

## 🧪 **Testing Both Environments**

### Local Development:
- Uses `run-local.ps1` environment variables
- Development environment settings
- Direct database connection

### Production (Render):
- Uses Render environment variables
- Production environment settings
- Secure, managed database connection

## 📁 **File Summary**

| File | Purpose | Contains Secrets |
|------|---------|------------------|
| `appsettings.json` | Base config | ❌ No |
| `appsettings.Development.json` | Dev config | ❌ No |
| `appsettings.Production.json` | Prod config | ❌ No |
| `run-local.ps1` | Local env setup | ⚠️ Yes (gitignored) |
| `run-local.ps1.template` | Template | ❌ No |

## 🔍 **Verification**

To verify your setup is working:

```powershell
# 1. Check environment variable is set
$env:ConnectionStrings__DefaultConnection

# 2. Should return your database connection string
# 3. If empty, run .\run-local.ps1 first
```

## 🆘 **Troubleshooting**

### "ConnectionString property has not been initialized"
- **Cause**: Environment variable not set in current session
- **Fix**: Run `.\run-local.ps1` then `dotnet run`

### "File not found: run-local.ps1"
- **Cause**: Script was gitignored or deleted
- **Fix**: Copy from `run-local.ps1.template` and update connection string

### Application won't start
- **Check**: Environment variables are set
- **Check**: Database is accessible
- **Check**: No syntax errors in appsettings files

---

## ✅ **You're All Set!**

Your application is now configured for:
- ✅ **Secure local development** (environment variables)
- ✅ **Secure production deployment** (Render environment variables)
- ✅ **No sensitive data in repository** (all secrets via environment)
- ✅ **Easy team sharing** (template files provided)

**Happy coding!** 🎉
