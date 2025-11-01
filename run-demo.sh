#!/bin/bash

# HelloblueGK Demo Runner Script
# This script runs the Web API demo with Swagger UI

echo "🚀 Starting HelloblueGK Demo..."
echo ""

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 9.0 SDK:"
    echo "   https://dotnet.microsoft.com/download"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version)
echo "✅ Found .NET SDK: $DOTNET_VERSION"
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
dotnet restore

# Build the solution
echo "🔨 Building solution..."
dotnet build --configuration Release

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
    dotnet run
else
    # Use the main project - we'll need to modify it to support web mode
    # For now, just run the console app and inform user
    echo "⚠️  WebAPI project not fully configured. Running main application..."
    echo "   To run as Web API, ensure WebAPI/Program.cs is configured."
    dotnet run --project HelloblueGK.csproj
fi

