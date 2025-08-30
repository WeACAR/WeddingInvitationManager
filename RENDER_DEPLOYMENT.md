# Wedding Invitation Manager - Render Deployment Guide

## 🚀 Quick Deploy to Render

This project is configured for easy deployment to Render using Docker.

### Prerequisites
- Render account (https://render.com)
- GitHub repository with this code
- PostgreSQL database (can be created on Render)

## 🔧 Deployment Steps

### 1. Create PostgreSQL Database on Render
1. Go to your Render Dashboard
2. Click "New" → "PostgreSQL"
3. Name it `wedding-invitation-db`
4. Choose your plan (Free tier available)
5. Copy the **Internal Database URL** for later

### 2. Deploy the Web Service
1. Click "New" → "Web Service"
2. Connect your GitHub repository
3. Choose the repository: `WeddingInvitationManager`
4. Configure:
   - **Name**: `wedding-invitation-manager`
   - **Environment**: `Docker`
   - **Dockerfile Path**: `./Dockerfile`
   - **Branch**: `main`

### 3. Set Environment Variables
In the Render dashboard, add these environment variables:

#### Required (Sensitive - Add as Secrets):
```
ConnectionStrings__DefaultConnection = [Your PostgreSQL Internal Database URL]
```

#### Optional (for WhatsApp integration):
```
WhatsApp__ApiKey = [Your WhatsApp API Key]
WhatsApp__BusinessNumber = [Your Business Phone Number]
```

#### Pre-configured (already in render.yaml):
- `ASPNETCORE_ENVIRONMENT` = Production
- `ASPNETCORE_URLS` = http://+:80
- `AllowedHosts` = *
- `Logging__LogLevel__Default` = Information
- All file upload settings

### 4. Deploy
1. Click "Create Web Service"
2. Render will automatically build and deploy your application
3. First deployment takes 5-10 minutes

## 🔒 Security Features

### ✅ What's Secured:
- **No sensitive data** in repository
- **Environment variables** used for all secrets
- **PostgreSQL connection** via environment variable
- **HTTPS** automatically provided by Render
- **Proper .gitignore** excludes sensitive files

### ✅ Production Ready:
- **Docker containerized** for consistency
- **Health checks** configured
- **Persistent storage** for uploads
- **Auto-deploy** on git push
- **Production logging** configured

## 📁 File Structure
```
├── Dockerfile              # Docker configuration
├── .dockerignore           # Files excluded from Docker build
├── render.yaml             # Render service configuration
├── WeddingInvitationManager/
│   ├── appsettings.json           # Development settings (no secrets)
│   ├── appsettings.Production.json # Production settings (no secrets)
│   └── ...                        # Application files
```

## 🌐 Custom Domain (Optional)
1. In Render dashboard, go to your service
2. Click "Settings" → "Custom Domains"
3. Add your domain and configure DNS

## 📊 Monitoring
- **Logs**: Available in Render dashboard
- **Metrics**: CPU, memory usage in dashboard
- **Health checks**: Automatic monitoring at `/`

## 🔄 Automatic Updates
- **Auto-deploy**: Pushes to `main` branch automatically deploy
- **Zero-downtime**: Render handles rolling deployments
- **Rollback**: Easy rollback in dashboard if needed

## 💰 Costs
- **Free Tier**: 750 hours/month (enough for personal projects)
- **PostgreSQL**: Free tier available with limitations
- **Paid Plans**: $7/month for starter, more for higher performance

## 🐛 Troubleshooting

### Build Fails
- Check Dockerfile syntax
- Ensure all dependencies in .csproj are compatible
- Check build logs in Render dashboard

### Database Connection Issues
- Verify `ConnectionStrings__DefaultConnection` environment variable
- Use **Internal Database URL** (not external)
- Check PostgreSQL service is running

### Application Won't Start
- Check environment variables are set correctly
- Review application logs in Render dashboard
- Ensure `ASPNETCORE_URLS=http://+:80`

## 📞 Support
- **Render Docs**: https://render.com/docs
- **Render Community**: https://community.render.com

---

## Example Database URL Format
```
postgresql://username:password@hostname:port/database
```

Your Internal Database URL from Render will look like:
```
postgresql://wedding_invitation_db_user:xxxxx@dpg-xxxxx-a/wedding_invitation_db
```
