# 🚀 Deploy to Render - Production Deployment Guide

## Quick Deploy (15 Minutes to Production!)

### Step 1: Prepare Your Repository

Make sure your code is pushed to GitHub:
```bash
git add .
git commit -m "Production-ready deployment"
git push origin main
```

### Step 2: Deploy to Render

#### Option A: Using Render Web UI (Easiest - Recommended)

1. **Go to Render Dashboard**
   - Visit: https://dashboard.render.com
   - Sign in or create free account

2. **Create New Web Service**
   - Click **"New +"** button (top right)
   - Select **"Web Service"**

3. **Connect GitHub Repository**
   - Connect your GitHub account (if not already)
   - Select repository: `HelloblueAI/HelloblueGK`
   - Click **"Connect"**

4. **Configure Service** (Copy these exact values):

   **Basic Settings:**
   - **Name:** `hellobluegk-production`
   - **Region:** `Oregon (US West)` or closest to you
   - **Branch:** `main`
   - **Root Directory:** (leave empty)

   **Build & Deploy:**
   - **Runtime:** `Docker`
   - **Dockerfile Path:** `Dockerfile.render`
   - **Docker Context:** `.` (dot)
   - **Build Command:** (leave empty - Docker handles it)
   - **Start Command:** (leave empty - Docker handles it)

   **Environment Variables** (Click "Add Environment Variable" for each):
   ```
   Key: ASPNETCORE_ENVIRONMENT
   Value: Production
   
   Key: ASPNETCORE_URLS
   Value: http://0.0.0.0:$PORT
   
   Key: DOTNET_SKIP_FIRST_TIME_EXPERIENCE
   Value: true
   
   Key: DOTNET_CLI_TELEMETRY_OPTOUT
   Value: true
   ```

   **Advanced Settings:**
   - **Health Check Path:** `/Health`
   - **Auto-Deploy:** `Yes` ✅
   - **Plan:** `Free` (upgrade later if needed)

5. **Create Service**
   - Click **"Create Web Service"**
   - Wait 5-10 minutes for first build
   - Watch the build logs in real-time!

6. **Get Your Live URL!**
   - Once deployed: `https://hellobluegk-production.onrender.com`
   - Swagger UI: `https://hellobluegk-production.onrender.com/swagger`
   - Health Check: `https://hellobluegk-production.onrender.com/Health`
   - Metrics: `https://hellobluegk-production.onrender.com/metrics`

---

#### Option B: Using Render CLI (Advanced)

```bash
# Install Render CLI
curl -fsSL https://render.com/install-cli.sh | sh

# Login to Render
render login

# Deploy using render.yaml (automatic configuration)
render deploy

# OR create service manually
render services create web \
  --name hellobluegk-production \
  --dockerfile-path Dockerfile.render \
  --docker-context . \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env ASPNETCORE_URLS=http://0.0.0.0:\$PORT \
  --health-check-path /Health \
  --auto-deploy true
```

---

## ✅ After Deployment

### 1. Test Your Deployment

```bash
# Health check
curl https://hellobluegk-production.onrender.com/Health

# Swagger UI
open https://hellobluegk-production.onrender.com/swagger

# Metrics
curl https://hellobluegk-production.onrender.com/metrics
```

### 2. Test Authentication

1. Open Swagger UI
2. Use `POST /api/v1/auth/register` to create a user
3. Use `POST /api/v1/auth/login` to get a token
4. Click "Authorize" and paste: `Bearer YOUR_TOKEN`
5. Test protected endpoints!

### 3. Update Documentation

Update your README.md with your live URL:
```markdown
**🚀 Live Demo:** https://hellobluegk-production.onrender.com
- **Swagger UI:** https://hellobluegk-production.onrender.com/swagger
- **Health Check:** https://hellobluegk-production.onrender.com/Health
```

---

## 🎉 What You Get

### Free Tier Includes:
- ✅ **Always-on service** (may sleep after 15 min inactivity on free tier)
- ✅ **HTTPS/SSL** - Automatic SSL certificate
- ✅ **Auto-deploy** - Deploys on every git push
- ✅ **Build logs** - See what's happening
- ✅ **Health checks** - Automatic monitoring
- ✅ **Custom domain** - Add your own domain (free)

### Production Features:
- ✅ **Global CDN** - Fast worldwide
- ✅ **Auto-scaling** - Handles traffic spikes
- ✅ **Zero maintenance** - Render manages everything
- ✅ **Professional** - Enterprise-grade infrastructure

---

## 🔧 Configuration Details

### Environment Variables

Your app uses these environment variables (set automatically by Render):
- `PORT` - Set by Render (don't set manually)
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://0.0.0.0:$PORT`

### Database

On first run, the app creates a SQLite database automatically (for development).
For production, you can:
1. Add a PostgreSQL database in Render (free tier available)
2. Update connection string in environment variables
3. Run migrations (if using EF migrations)

### Custom Domain (Optional)

1. Go to your service settings
2. Click "Custom Domains"
3. Add your domain
4. Update DNS records as instructed
5. SSL certificate is automatic!

---

## 📊 Monitoring

### View Logs
- Go to Render Dashboard → Your Service → Logs
- Real-time logs
- Historical logs

### Metrics
- Visit: `https://your-app.onrender.com/metrics`
- Prometheus format
- Can integrate with Grafana

### Health Checks
- Render automatically checks `/Health` endpoint
- Service restarts if unhealthy
- View status in dashboard

---

## 🚀 Auto-Deploy

Every time you push to `main` branch:
1. Render detects the push
2. Automatically builds new version
3. Deploys when build succeeds
4. Your app is updated! (Zero downtime)

**To disable auto-deploy:**
- Go to service settings
- Toggle "Auto-Deploy" off

---

## 💰 Pricing

### Free Tier (Perfect to Start)
- ✅ 750 hours/month free
- ✅ 512MB RAM
- ✅ 0.5 CPU
- ✅ Sleeps after 15 min inactivity (wakes on request)
- ✅ Perfect for demos, MVPs, production apps

### Paid Plans (When You Scale)
- **Starter:** $7/month - Always on, 512MB RAM
- **Standard:** $25/month - 2GB RAM, better performance
- **Pro:** Custom - For high traffic

**Upgrade when you need:**
- Always-on (no sleep)
- More resources
- Higher traffic limits

---

## 🐛 Troubleshooting

### Build Fails

**Check:**
1. Build logs in Render dashboard
2. Ensure Dockerfile.render exists
3. Verify .NET 9.0 is available
4. Check for syntax errors in code

### App Won't Start

**Check:**
1. Start command is correct
2. PORT environment variable is used
3. Health check endpoint works
4. Database connection (if using)

### Can't Access Swagger

**Check:**
1. Swagger is enabled in Production (it is!)
2. Health endpoint works: `/Health`
3. Environment variables are set
4. Check service logs

### Service Keeps Restarting

**Check:**
1. Health check endpoint: `/Health`
2. Application logs for errors
3. Database connection issues
4. Memory limits (upgrade plan if needed)

---

## 🎯 Next Steps After Deployment

1. **Test all endpoints** - Use Swagger UI
2. **Set up monitoring** - Connect Prometheus/Grafana
3. **Add custom domain** - Make it yours
4. **Configure database** - Add PostgreSQL if needed
5. **Set up CI/CD** - Already done! (auto-deploy)
6. **Share your API** - Add to portfolio, social media

---

## 🏆 Success Checklist

- [ ] Service deployed successfully
- [ ] Health check returns 200 OK
- [ ] Swagger UI accessible
- [ ] Can register/login users
- [ ] Protected endpoints work with JWT
- [ ] Metrics endpoint working
- [ ] Auto-deploy enabled
- [ ] Custom domain added (optional)
- [ ] README updated with live URL

---

## 📚 Additional Resources

- **Render Docs:** https://render.com/docs
- **.NET on Render:** https://render.com/docs/deploy-dotnet
- **Support:** support@render.com

---

**Your production API is now live and professional!** 🚀

*Deployment time: ~15 minutes*  
*Maintenance: Zero*  
*Cost: Free to start*  
*Quality: Enterprise-grade*

