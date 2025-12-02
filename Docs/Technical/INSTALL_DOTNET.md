# Installing .NET 9.0 SDK

The demo requires .NET 9.0 SDK to run. Here's how to install it:

## Quick Install (Linux)

### Option 1: Install via Package Manager (Ubuntu/Debian)

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 9.0 SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0
```

### Option 2: Install via Script (Recommended)

```bash
# Download and run the install script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0

# Add to PATH (add this to your ~/.zshrc to make it permanent)
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

# Reload shell
source ~/.zshrc
```

### Option 3: Manual Download

1. Visit: https://dotnet.microsoft.com/download/dotnet/9.0
2. Download the Linux x64 SDK
3. Extract and add to PATH

## Verify Installation

After installation, verify it works:

```bash
dotnet --version
# Should show: 9.0.x

dotnet --info
# Shows detailed installation info
```

## Add to PATH Permanently

Add these lines to your `~/.zshrc`:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

Then reload:
```bash
source ~/.zshrc
```

## Troubleshooting

### If dotnet is still not found after installation:

1. Check if it's installed:
   ```bash
   ls -la ~/.dotnet/dotnet
   ```

2. If it exists, add to PATH:
   ```bash
   export PATH="$HOME/.dotnet:$PATH"
   ```

3. Make it permanent by adding to `~/.zshrc`

### Check Installation Location

```bash
find ~ -name "dotnet" -type f 2>/dev/null
```

---

**Once installed, you can run the demo:**

```bash
cd WebAPI
dotnet restore
dotnet run
```

Then open: http://localhost:5000/swagger

