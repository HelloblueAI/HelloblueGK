# 🔧 Install GitHub CLI

## Quick Installation for Ubuntu/Debian

Run these commands in your terminal:

```bash
# Add GitHub CLI repository
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | sudo dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg
sudo chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg

# Add repository to sources
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null

# Update package list
sudo apt update

# Install GitHub CLI
sudo apt install gh -y
```

## Verify Installation

After installation, verify it works:

```bash
gh --version
```

You should see something like:
```
gh version 2.x.x (yyyy-mm-dd)
https://github.com/cli/cli/releases/tag/v2.x.x
```

## Authenticate

After installation, authenticate with GitHub:

```bash
gh auth login
```

Follow the prompts:
1. Choose "GitHub.com"
2. Choose your preferred authentication method (browser or token)
3. Follow the instructions

## After Installation

Once GitHub CLI is installed and authenticated, you can run:

```bash
# Complete setup (Codecov + Branch Protection)
./.github/setup-all.sh

# Or individually:
./.github/setup-codecov.sh
./.github/setup-branch-protection.sh
```

## Alternative Installation Methods

### Using Snap (if available)
```bash
sudo snap install gh
```

### Using Homebrew (if installed)
```bash
brew install gh
```

### Manual Download
Visit: https://github.com/cli/cli/releases

---

**After installation**: Run `gh auth login` to authenticate

