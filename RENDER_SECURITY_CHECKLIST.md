# Security & Deployment Checklist for Render

## ✅ Security Verification

### Repository Security
- [ ] ✅ **No sensitive data in appsettings.json** - Connection string is empty
- [ ] ✅ **No sensitive data in appsettings.Production.json** - All secrets empty
- [ ] ✅ **Proper .gitignore** - Excludes .env files and sensitive data
- [ ] ✅ **Environment variables setup** - All configuration via env vars
- [ ] ✅ **No hardcoded passwords or API keys** in source code

### Docker Security
- [ ] ✅ **Dockerfile uses official Microsoft images**
- [ ] ✅ **Non-root user execution** (ASP.NET Core default)
- [ ] ✅ **Minimal attack surface** - Only necessary packages installed
- [ ] ✅ **Proper file permissions** set for upload directories
- [ ] ✅ **.dockerignore** excludes sensitive files

### Render Configuration
- [ ] ✅ **Environment variables** configured for secrets
- [ ] ✅ **Health checks** enabled
- [ ] ✅ **HTTPS** automatically provided by Render
- [ ] ✅ **Auto-deployment** configured
- [ ] ✅ **Persistent storage** for uploads

## 🚀 Deployment Steps for Render

### 1. Prepare Repository
```bash
# Ensure all files are committed
git add .
git commit -m "Add Docker and Render configuration"
git push origin main
```

### 2. Create PostgreSQL Database on Render
1. Go to Render Dashboard
2. New → PostgreSQL
3. Name: `wedding-invitation-db`
4. Plan: Free or Starter
5. Copy the **Internal Database URL**

### 3. Create Web Service
1. New → Web Service
2. Connect GitHub repository
3. Configure:
   - **Name**: `wedding-invitation-manager`
   - **Environment**: Docker
   - **Dockerfile Path**: `./Dockerfile`
   - **Branch**: `main`
   - **Build Command**: (leave empty)
   - **Start Command**: (leave empty)

### 4. Set Environment Variables in Render Dashboard

#### Critical (Set as Environment Variables):
```
ConnectionStrings__DefaultConnection = [Your PostgreSQL Internal Database URL]
```

#### Optional (for WhatsApp):
```
WhatsApp__ApiKey = [Your WhatsApp API Key]
WhatsApp__BusinessNumber = [Your Business Number]
```

#### Auto-configured (from render.yaml):
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:80`
- `AllowedHosts=*`
- All logging and file upload settings

### 5. Deploy
Click "Create Web Service" - Render will build and deploy automatically.

## 🧪 Local Testing (Optional)

Before deploying, test locally with Docker:

```powershell
# Test Docker build
.\test-docker.ps1

# Or manually:
docker build -t wedding-invitation-manager .
docker-compose up -d

# Access at http://localhost:5000
```

## 🔒 Production Security Best Practices

### Database Security
- ✅ Use Render's managed PostgreSQL (encrypted, backed up)
- ✅ Use Internal Database URL (not accessible from internet)
- ✅ Connection string only in environment variables

### Application Security
- ✅ HTTPS enforced by Render
- ✅ Environment-based configuration
- ✅ No secrets in source code
- ✅ Regular dependency updates

### File Upload Security
- ✅ File type restrictions configured
- ✅ File size limits enforced
- ✅ Persistent storage with proper permissions

## 📊 Monitoring & Maintenance

### Available in Render Dashboard:
- **Real-time logs** - Application and system logs
- **Metrics** - CPU, memory, disk usage
- **Health checks** - Automatic monitoring
- **Deploy history** - Easy rollbacks
- **Custom domains** - Add your own domain

### Recommended Monitoring:
- Set up **email notifications** for failures
- Monitor **disk usage** for uploads
- Check **database size** regularly
- Review **application logs** for errors

## 💰 Cost Estimation

### Free Tier (Good for testing):
- **Web Service**: 750 hours/month free
- **PostgreSQL**: 1GB storage, limited connections
- **Total**: $0/month

### Production (Recommended):
- **Web Service**: Starter plan $7/month
- **PostgreSQL**: Starter plan $7/month
- **Total**: ~$14/month

## 🆘 Troubleshooting

### Common Issues:
1. **Build fails**: Check Dockerfile syntax and dependencies
2. **Database connection**: Verify environment variable and database status
3. **File uploads fail**: Check disk storage and permissions
4. **Performance issues**: Consider upgrading to higher tier

### Support Resources:
- **Render Documentation**: https://render.com/docs
- **Render Community**: https://community.render.com
- **GitHub Issues**: Create issues in your repository

---

## 🎉 Ready for Production!

Your Wedding Invitation Manager is now configured for secure, professional deployment on Render with:

- ✅ **Docker containerization**
- ✅ **Zero sensitive data exposure**
- ✅ **Production-ready configuration**
- ✅ **Automatic deployments**
- ✅ **Scalable architecture**

Deploy with confidence! 🚀
