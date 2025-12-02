# Render Setup with Docker

Since Render doesn't have .NET in the language dropdown, use **Docker**.

## Render Form Settings

### Basic Settings
- **Language:** `Docker` ✅
- **Name:** `hellobluegk-demo`
- **Branch:** `main`
- **Region:** `Oregon (US West)` (or your preference)
- **Root Directory:** Leave **EMPTY** (use root of repo)
- **Instance Type:** `Free` (for demo)

### Build & Deploy
- **Dockerfile Path:** `Docker/Dockerfile.render`
- **Docker Build Context:** `.` (root directory)

### Environment Variables
Add these:
1. `ASPNETCORE_ENVIRONMENT` = `Production`
2. `ASPNETCORE_URLS` = `http://0.0.0.0:$PORT`

### Advanced
- **Health Check Path:** `/Health`
- **Auto-Deploy:** `Yes`

## Dockerfile Location

The Dockerfile should be in the **root directory** (not WebAPI folder).

We created `Docker/Dockerfile.render` - use that!

## After Deployment

Your URL will be: `https://hellobluegk-demo.onrender.com`
Swagger UI: `https://hellobluegk-demo.onrender.com/swagger`

