# Production Setup Guide

## 🚀 Setting Up Production Service

This guide will help you set up HelloblueGK to run as a production service that:
- ✅ Always runs (even after reboot)
- ✅ Auto-starts on server boot
- ✅ Auto-restarts if it crashes
- ✅ Runs in background

---

## Step 1: Build for Production

First, build the application in Release mode:

```bash
cd /home/pejmanhaghighatnia/Documents/PicoGK/WebAPI
dotnet publish -c Release -o ./publish
```

This creates optimized production binaries in the `publish` folder.

---

## Step 2: Install as Systemd Service

### Option A: Install Systemd Service (Recommended)

1. **Copy service file:**
```bash
sudo cp /home/pejmanhaghighatnia/Documents/PicoGK/WebAPI/hellobluegk.service /etc/systemd/system/
```

2. **Update the service file** with correct paths:
```bash
sudo nano /etc/systemd/system/hellobluegk.service
```

Update these paths:
- `WorkingDirectory` - Your WebAPI directory
- `ExecStart` - Path to your published DLL
- `User` - Your username

3. **Reload systemd:**
```bash
sudo systemctl daemon-reload
```

4. **Enable service** (auto-start on boot):
```bash
sudo systemctl enable hellobluegk
```

5. **Start service:**
```bash
sudo systemctl start hellobluegk
```

6. **Check status:**
```bash
sudo systemctl status hellobluegk
```

### Service Management Commands

```bash
# Start service
sudo systemctl start hellobluegk

# Stop service
sudo systemctl stop hellobluegk

# Restart service
sudo systemctl restart hellobluegk

# Check status
sudo systemctl status hellobluegk

# View logs
sudo journalctl -u hellobluegk -f

# Disable auto-start
sudo systemctl disable hellobluegk
```

---

## Step 3: Configure Production Settings

1. **Update appsettings.Production.json:**
```bash
cd /home/pejmanhaghighatnia/Documents/PicoGK/WebAPI
cp appsettings.Production.json.example appsettings.Production.json
nano appsettings.Production.json
```

2. **Update important settings:**
- JWT Key (use a secure random string)
- Database connection string
- CORS origins
- Logging levels

3. **Set environment variable:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
```

Or add to service file:
```ini
Environment=ASPNETCORE_ENVIRONMENT=Production
```

---

## Step 4: Configure Firewall (if needed)

```bash
# Allow port 5000
sudo ufw allow 5000/tcp

# Or for specific IP
sudo ufw allow from YOUR_IP to any port 5000
```

---

## Step 5: Set Up Reverse Proxy (Optional but Recommended)

### Using Nginx

1. **Install Nginx:**
```bash
sudo apt update
sudo apt install nginx
```

2. **Create Nginx config:**
```bash
sudo nano /etc/nginx/sites-available/hellobluegk
```

Add:
```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

3. **Enable site:**
```bash
sudo ln -s /etc/nginx/sites-available/hellobluegk /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Step 6: Set Up SSL (Optional but Recommended)

### Using Let's Encrypt

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d your-domain.com
```

---

## Production Checklist

- [ ] Application built in Release mode
- [ ] Systemd service installed and enabled
- [ ] Service starts successfully
- [ ] Production appsettings.json configured
- [ ] JWT key changed from default
- [ ] Database connection string configured
- [ ] Firewall configured
- [ ] Reverse proxy set up (optional)
- [ ] SSL certificate installed (optional)
- [ ] Logs are being monitored
- [ ] Backup strategy in place

---

## Monitoring

### View Logs
```bash
# Real-time logs
sudo journalctl -u hellobluegk -f

# Last 100 lines
sudo journalctl -u hellobluegk -n 100

# Logs since today
sudo journalctl -u hellobluegk --since today
```

### Check Health
```bash
curl http://localhost:5000/Health
```

### Monitor Metrics
```bash
curl http://localhost:5000/metrics
```

---

## Troubleshooting

### Service won't start
```bash
# Check status
sudo systemctl status hellobluegk

# Check logs
sudo journalctl -u hellobluegk -n 50

# Verify paths in service file
cat /etc/systemd/system/hellobluegk.service
```

### Port already in use
```bash
# Find what's using port 5000
sudo lsof -i :5000

# Kill the process or change port in appsettings
```

### Permission issues
```bash
# Check file permissions
ls -la /home/pejmanhaghighatnia/Documents/PicoGK/WebAPI

# Ensure user in service file matches
```

---

## Quick Start Script

I've created a setup script. Run:

```bash
cd /home/pejmanhaghighatnia/Documents/PicoGK/WebAPI
chmod +x setup-production.sh
./setup-production.sh
```

---

**Your application will now run 24/7 as a production service!** 🚀

