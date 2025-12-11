# Railway Deployment Fix

## Issue: Health Check Failing

Railway is failing health checks because the application isn't starting. The most likely cause is **missing environment variables**, especially the database connection string.

## Required Environment Variables for Railway

In Railway dashboard, go to your service → **Variables** tab and add:

### Required Variables:

```bash
# Application Environment
ASPNETCORE_ENVIRONMENT=Production

# Port (Railway sets this automatically, but you can override)
PORT=5000

# Database Connection (REQUIRED - app will crash without this in production)
ConnectionStrings__DefaultConnection=your-postgresql-connection-string

# JWT Authentication (REQUIRED - app will crash with default key in production)
Jwt__Key=your-secure-jwt-key-minimum-32-characters-long
Jwt__Issuer=hellobluegk
Jwt__Audience=hellobluegk-api
```

### Optional but Recommended:

```bash
# Performance
DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
DOTNET_CLI_TELEMETRY_OPTOUT=true
```

## Quick Fix Steps

### 1. Add Database Connection

**Option A: Use Railway PostgreSQL**
1. In Railway, create a **PostgreSQL** service
2. Railway will automatically create a `DATABASE_URL` environment variable
3. Convert it to .NET format:
   ```
   # Railway gives you: postgresql://user:pass@host:port/db
   # Convert to: Host=host;Port=port;Database=db;Username=user;Password=pass
   ```

**Option B: Use External PostgreSQL**
- Add `ConnectionStrings__DefaultConnection` with your PostgreSQL connection string

### 2. Add JWT Key

Generate a secure key:
```bash
# Generate secure 32+ character key
openssl rand -base64 32
```

Add as `Jwt__Key` environment variable.

### 3. Redeploy

After adding environment variables, Railway will automatically redeploy.

## Railway-Specific Configuration

Railway automatically:
- Sets `PORT` environment variable
- Provides `DATABASE_URL` if you use Railway PostgreSQL
- Routes traffic to your service

Your app already reads `PORT` correctly (line 280 in Program.cs).

## Troubleshooting

### Check Railway Logs

1. Go to Railway dashboard
2. Click your service
3. Click **Deployments** tab
4. Click latest deployment
5. Check **Build Logs** and **Deploy Logs**

### Common Issues:

1. **"DefaultConnection string must be configured"**
   - Add `ConnectionStrings__DefaultConnection` environment variable

2. **"Default JWT key detected in production"**
   - Add `Jwt__Key` environment variable with secure key

3. **Database connection timeout**
   - Check PostgreSQL service is running
   - Verify connection string format
   - Check if database is sleeping (free tier)

4. **Health check failing**
   - App might be crashing on startup
   - Check deploy logs for errors
   - Increase health check timeout in railway.json

## Updated railway.json

I've updated `railway.json` with:
- Increased health check timeout (300 seconds)
- Better restart policy
- Health check path configuration

## After Fixing

Once environment variables are set:
1. Railway will auto-redeploy
2. Health checks should pass
3. Service will be live

## Test After Deployment

```bash
# Health check
curl https://hellobluegk-demo-production.up.railway.app/Health

# Swagger
open https://hellobluegk-demo-production.up.railway.app/swagger
```
