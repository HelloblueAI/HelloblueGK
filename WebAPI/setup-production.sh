#!/bin/bash

# Production Setup Script for HelloblueGK
# This script sets up the application as a systemd service

set -e

echo "🚀 HelloblueGK Production Setup"
echo "================================"
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Get current directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR"
PUBLISH_DIR="$PROJECT_DIR/publish"

echo "📦 Step 1: Building application for production..."
cd "$PROJECT_DIR"
dotnet publish -c Release -o "$PUBLISH_DIR"

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed!${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Build successful!${NC}"
echo ""

# Get the DLL path
DLL_PATH="$PUBLISH_DIR/HelloblueGK.WebAPI.dll"

if [ ! -f "$DLL_PATH" ]; then
    echo -e "${RED}❌ Published DLL not found at $DLL_PATH${NC}"
    exit 1
fi

echo "📝 Step 2: Creating systemd service file..."
echo ""

# Get dotnet path
DOTNET_PATH=$(which dotnet)
if [ -z "$DOTNET_PATH" ]; then
    echo -e "${RED}❌ dotnet not found in PATH${NC}"
    exit 1
fi

# Get current user
CURRENT_USER=$(whoami)

# Create service file content
SERVICE_FILE="/tmp/hellobluegk.service"
cat > "$SERVICE_FILE" << EOF
[Unit]
Description=HelloblueGK Aerospace Engine Simulation API
After=network.target

[Service]
Type=notify
WorkingDirectory=$PUBLISH_DIR
ExecStart=$DOTNET_PATH $DLL_PATH
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=hellobluegk
User=$CURRENT_USER
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=$(dirname $DOTNET_PATH)/..

[Install]
WantedBy=multi-user.target
EOF

echo "Service file created at: $SERVICE_FILE"
echo ""
cat "$SERVICE_FILE"
echo ""

echo "⚠️  Step 3: Installing systemd service..."
echo "This requires sudo privileges."
echo ""

read -p "Do you want to install the service? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Installation cancelled."
    echo "Service file is at: $SERVICE_FILE"
    echo "You can manually copy it to /etc/systemd/system/hellobluegk.service"
    exit 0
fi

# Copy service file
sudo cp "$SERVICE_FILE" /etc/systemd/system/hellobluegk.service

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Failed to copy service file${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Service file installed${NC}"
echo ""

# Reload systemd
echo "🔄 Reloading systemd daemon..."
sudo systemctl daemon-reload

# Enable service
echo "🔧 Enabling service (auto-start on boot)..."
sudo systemctl enable hellobluegk

# Start service
echo "▶️  Starting service..."
sudo systemctl start hellobluegk

# Wait a moment
sleep 2

# Check status
echo ""
echo "📊 Service Status:"
echo "=================="
sudo systemctl status hellobluegk --no-pager

echo ""
echo -e "${GREEN}✅ Production setup complete!${NC}"
echo ""
echo "📋 Useful commands:"
echo "  sudo systemctl status hellobluegk    - Check status"
echo "  sudo systemctl restart hellobluegk  - Restart service"
echo "  sudo journalctl -u hellobluegk -f   - View logs"
echo ""
echo "🌐 Your API should be running at: http://localhost:5000"
echo "📚 Swagger UI: http://localhost:5000/swagger"
echo ""

