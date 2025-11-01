# Deploy to Render - Step by Step

## Option 1: Using Render Web UI (Easiest - Recommended)

1. **Go to Render Dashboard**
   - Visit: https://dashboard.render.com
   - Sign in or create account

2. **Create New Web Service**
   - Click "New +" button
   - Select "Web Service"

3. **Connect Repository**
   - Connect your GitHub account if not already connected
   - Select repository: `HelloblueAI/HelloblueGK`
   - Click "Connect"

4. **Configure Service**
   - **Name:** `hellobluegk-demo`
   - **Region:** Choose closest to you (e.g., `Oregon (US West)`)
   - **Branch:** `main`
   - **Root Directory:** Leave empty (or set to `WebAPI` if option available)
   - **Runtime:** `.NET`
   - **Build Command:** `cd WebAPI && dotnet publish -c Release -o ./publish`
   - **Start Command:** `cd WebAPI/publish && dotnet HelloblueGK.WebAPI.dll`
   - **Environment Variables:**
     - `ASPNETCORE_ENVIRONMENT` = `Production`
     - `ASPNETCORE_URLS` = `http://0.0.0.0:$PORT`

5. **Advanced Settings**
   - **Health Check Path:** `/Health`
   - **Auto-Deploy:** `Yes` (deploys on every push)

6. **Create Service**
   - Click "Create Web Service"
   - Wait for build to complete (5-10 minutes)

7. **Get Your URL**
   - Once deployed, you'll get a URL like: `https://hellobluegk-demo.onrender.com`
   - Your Swagger UI will be at: `https://hellobluegk-demo.onrender.com/swagger`

## Option 2: Using Render CLI

```bash
# Install Render CLI (if not installed)
curl -fsSL https://render.com/install-cli.sh | sh

# Login
render login

# Deploy using render.yaml
render deploy

# OR create service manually
render services create web \
  --name hellobluegk-demo \
  --runtime dotnet \
  --root-dir WebAPI \
  --build-command "dotnet publish -c Release -o ./publish" \
  --start-command "cd publish && dotnet HelloblueGK.WebAPI.dll" \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env ASPNETCORE_URLS=http://0.0.0.0:\$PORT \
  --health-check-path /Health
```

## After Deployment

1. **Test the deployment:**
   - Visit: `https://your-app.onrender.com/swagger`
   - Test a few endpoints

2. **Update README.md:**
   - Replace `[YOUR-RENDER-URL]` with your actual URL
   - Commit and push

3. **Share the link:**
   - Use in social media posts
   - Include in emails
   - Add to GitHub README

## Troubleshooting

**Build fails:**
- Check build logs in Render dashboard
- Ensure .NET 9.0 SDK is available (Render supports it)
- Verify build command paths are correct

**App won't start:**
- Check start command
- Verify PORT environment variable is used
- Check logs in Render dashboard

**Can't access Swagger:**
- Ensure Swagger is enabled in Production (we configured it)
- Check health check endpoint works: `/Health`
- Verify environment variables are set correctly

