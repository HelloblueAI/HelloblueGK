# Running the Application

## How `dotnet run` Works

### Development Mode (Current Setup)

**`dotnet run` is NOT always running** - you need to start it each time:

1. **Start the application:**
   ```bash
   cd WebAPI
   dotnet run
   ```

2. **Application runs** until you stop it (Ctrl+C)

3. **When you close terminal or press Ctrl+C**, the application stops

4. **To run again**, you need to run `dotnet run` again

---

## Development vs Production

### Development (What You're Doing Now)
- ✅ Run with `dotnet run` when you need it
- ✅ Stops when you close terminal
- ✅ Good for testing and development
- ✅ Auto-reloads on code changes (with `dotnet watch run`)

### Production (Deployed Applications)
- ✅ Runs as a service (always on)
- ✅ Auto-starts on server reboot
- ✅ Managed by systemd, Docker, or cloud services
- ✅ Runs in background

---

## Options for Running

### 1. **Manual Start (Current)**
```bash
cd WebAPI
dotnet run
```
- Starts when you run it
- Stops when you press Ctrl+C or close terminal
- **Use for**: Development, testing

### 2. **Background Process (Linux)**
```bash
cd WebAPI
nohup dotnet run > app.log 2>&1 &
```
- Runs in background
- Continues after you close terminal
- **Use for**: Temporary production-like testing

### 3. **Systemd Service (Production Linux)**
Create `/etc/systemd/system/hellobluegk.service`:
```ini
[Unit]
Description=HelloblueGK WebAPI
After=network.target

[Service]
Type=notify
WorkingDirectory=/home/pejmanhaghighatnia/Documents/PicoGK/WebAPI
ExecStart=/usr/bin/dotnet run
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Then:
```bash
sudo systemctl enable hellobluegk
sudo systemctl start hellobluegk
```
- **Always running**
- Auto-starts on boot
- **Use for**: Production servers

### 4. **Docker (Production)**
```bash
docker build -t hellobluegk .
docker run -d -p 5000:5000 hellobluegk
```
- Runs in container
- Always running
- **Use for**: Production, cloud deployment

### 5. **Watch Mode (Development with Auto-Reload)**
```bash
cd WebAPI
dotnet watch run
```
- Auto-restarts on code changes
- **Use for**: Active development

---

## Current Status

Right now, your application:
- ✅ **Is running** (because you ran `dotnet run`)
- ✅ **Will stop** when you press Ctrl+C or close terminal
- ✅ **Needs to be restarted** with `dotnet run` after stopping

---

## Quick Reference

| Command | When to Use | Always Running? |
|---------|-------------|-----------------|
| `dotnet run` | Development, testing | ❌ No - stops when you stop it |
| `dotnet watch run` | Active development | ❌ No - but auto-restarts on changes |
| `nohup dotnet run &` | Temporary background | ✅ Yes - until server reboot |
| Systemd service | Production Linux | ✅ Yes - always, auto-start |
| Docker | Production, cloud | ✅ Yes - always, managed by Docker |

---

## Recommendation

**For Development:**
- Use `dotnet run` when you need it
- Use `dotnet watch run` for active coding (auto-reloads)

**For Production:**
- Use systemd service (Linux)
- Use Docker container
- Use cloud platform (Render, Railway, Azure, AWS)

---

**Bottom Line**: `dotnet run` starts the app, but it's not always running. You need to start it each time (or set up a service for production).

