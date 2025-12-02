#!/bin/bash
# Helper script to find dotnet installation

echo "🔍 Searching for .NET SDK installation..."
echo ""

# Common locations
LOCATIONS=(
    "$HOME/.dotnet/dotnet"
    "/usr/local/bin/dotnet"
    "/usr/bin/dotnet"
    "/opt/dotnet/dotnet"
    "$HOME/.local/share/dotnet/dotnet"
    "/snap/dotnet-sdk/current/dotnet"
)

FOUND=false

for loc in "${LOCATIONS[@]}"; do
    if [ -f "$loc" ] && [ -x "$loc" ]; then
        echo "✅ Found: $loc"
        echo "   Version: $($loc --version 2>/dev/null || echo 'Unable to get version')"
        FOUND=true
        echo ""
        echo "To use it, run:"
        echo "  export PATH=\"$(dirname $loc):\$PATH\""
        echo "  OR"
        echo "  $loc restore"
        echo "  $loc run"
        break
    fi
done

if [ "$FOUND" = false ]; then
    echo "❌ .NET SDK not found in common locations"
    echo ""
    echo "Please install .NET 9.0 SDK:"
    echo "  https://dotnet.microsoft.com/download"
    echo ""
    echo "Or if already installed, add it to your PATH:"
    echo "  export PATH=\"\$HOME/.dotnet:\$PATH\""
    echo "  (Add this to your ~/.zshrc to make it permanent)"
fi

