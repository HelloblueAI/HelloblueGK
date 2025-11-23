#!/bin/bash

# HelloblueGK Demo Runner Script
# This script runs the Web API demo with Swagger UI

# Source shell profile to get dotnet in PATH (for zsh/bash)
if [ -f "$HOME/.zshrc" ]; then
    source "$HOME/.zshrc" 2>/dev/null || true
elif [ -f "$HOME/.bashrc" ]; then
    source "$HOME/.bashrc" 2>/dev/null || true
elif [ -f "$HOME/.profile" ]; then
    source "$HOME/.profile" 2>/dev/null || true
fi

echo "🚀 Starting HelloblueGK Demo..."
echo ""

# Check if .NET SDK is installed
# Try multiple methods to find dotnet
DOTNET_CMD=""
if command -v dotnet &> /dev/null; then
    DOTNET_CMD="dotnet"
elif [ -f "$HOME/.dotnet/dotnet" ]; then
    DOTNET_CMD="$HOME/.dotnet/dotnet"
elif [ -f "/usr/local/bin/dotnet" ]; then
    DOTNET_CMD="/usr/local/bin/dotnet"
elif [ -f "/usr/bin/dotnet" ]; then
    DOTNET_CMD="/usr/bin/dotnet"
fi

if [ -z "$DOTNET_CMD" ] || ! $DOTNET_CMD --version &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 9.0 SDK:"
    echo "   https://dotnet.microsoft.com/download"
    echo ""
    echo "   Or ensure dotnet is in your PATH"
    echo ""
    echo "   If dotnet is installed, try running manually:"
    echo "   cd WebAPI && dotnet run"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$($DOTNET_CMD --version)
echo "✅ Found .NET SDK: $DOTNET_VERSION (using: $DOTNET_CMD)"
echo ""

# Check if WebAPI directory exists
if [ ! -d "WebAPI" ]; then
    echo "❌ WebAPI directory not found. Creating it..."
    mkdir -p WebAPI/Controllers
fi

# Copy WebAPI Program.cs if it exists
if [ -f "WebAPI/Program.cs" ]; then
    echo "✅ Found WebAPI/Program.cs"
else
    echo "⚠️  WebAPI/Program.cs not found. Using main project..."
fi

# Restore dependencies
echo "📦 Restoring dependencies..."
$DOTNET_CMD restore

# Build the solution
echo "🔨 Building solution..."
$DOTNET_CMD build --configuration Release

# Check if build succeeded
if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check errors above."
    exit 1
fi

echo ""
echo "✅ Build successful!"
echo ""
echo "🌐 Starting Web API Server..."
echo "📚 Swagger UI will be available at: http://localhost:5000/swagger"
echo "🏥 Health Check: http://localhost:5000/Health"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

# Run the Web API if Program.cs exists in WebAPI, otherwise use main project
if [ -f "WebAPI/Program.cs" ] && [ -f "WebAPI/HelloblueGK.WebAPI.csproj" ]; then
    cd WebAPI
    $DOTNET_CMD run
else
    # Use the main project - we'll need to modify it to support web mode
    # For now, just run the console app and inform user
    echo "⚠️  WebAPI project not fully configured. Running main application..."
    echo "   To run as Web API, ensure WebAPI/Program.cs is configured."
    $DOTNET_CMD run --project HelloblueGK.csproj
fi

