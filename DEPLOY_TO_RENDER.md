# Deploy HelloblueGK to Render

## Quick Deploy (CLI)

```bash
# 1. Login to Render (if not already)
render login

# 2. Create service from render.yaml
render deploy

# OR manually create service:
render services create web \
  --name hellobluegk-demo \
  --runtime dotnet \
  --build-command "cd WebAPI && dotnet publish -c Release -o ./publish" \
  --start-command "cd WebAPI/publish && dotnet HelloblueGK.WebAPI.dll" \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env ASPNETCORE_URLS=http://0.0.0.0:\$PORT \
  --health-check-path /Health
```

## Manual Deploy (Web UI)

1. Go to https://render.com
2. Click "New +" → "Web Service"
3. Connect your GitHub repository
4. Configure:
   - **Name:** hellobluegk-demo
   - **Root Directory:** (leave empty, or set to `WebAPI`)
   - **Environment:** .NET
   - **Build Command:** `cd WebAPI && dotnet publish -c Release -o ./publish`
   - **Start Command:** `cd WebAPI/publish && dotnet HelloblueGK.WebAPI.dll`
   - **Environment Variables:**
     - `ASPNETCORE_ENVIRONMENT=Production`
     - `ASPNETCORE_URLS=http://0.0.0.0:$PORT`
5. Click "Create Web Service"

## After Deployment

Your demo will be available at:
`https://hellobluegk-demo.onrender.com/swagger`

Update README.md with this URL!

