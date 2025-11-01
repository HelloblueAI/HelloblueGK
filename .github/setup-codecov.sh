#!/bin/bash
# Codecov Setup Automation Script
# This script helps automate Codecov integration setup

set -e

REPO="HelloblueAI/HelloblueGK"
CODECOV_URL="https://codecov.io"

echo "🎯 Codecov Integration Setup"
echo "=============================="
echo ""

# Check if we have GitHub CLI
if command -v gh &> /dev/null; then
    echo "✅ GitHub CLI detected"
    echo ""
    echo "📋 Step 1: Opening Codecov website..."
    echo "   Please sign in with GitHub and add the repository: $REPO"
    echo ""
    
    # Try to open browser
    if command -v xdg-open &> /dev/null; then
        xdg-open "$CODECOV_URL" 2>/dev/null || true
    elif command -v open &> /dev/null; then
        open "$CODECOV_URL" 2>/dev/null || true
    fi
    
    echo "⏳ Waiting for you to complete Codecov setup..."
    echo "   After adding the repo, Codecov will show you a token."
    echo ""
    read -p "Press Enter after you've copied the Codecov token..."
    
    echo ""
    echo "📋 Step 2: Adding token to GitHub Secrets..."
    echo ""
    read -p "Paste your Codecov token here: " CODECOV_TOKEN
    
    if [ -z "$CODECOV_TOKEN" ]; then
        echo "❌ No token provided. Exiting."
        exit 1
    fi
    
    echo ""
    echo "🔐 Adding CODECOV_TOKEN secret to GitHub..."
    
    # Add secret using GitHub CLI
    if gh secret set CODECOV_TOKEN --body "$CODECOV_TOKEN" --repo "$REPO" 2>/dev/null; then
        echo "✅ Codecov token added successfully!"
        echo ""
        echo "🎉 Setup complete! Next CI/CD run will upload coverage to Codecov."
    else
        echo "❌ Failed to add secret. You may need to:"
        echo "   1. Run: gh auth login"
        echo "   2. Or add the secret manually via GitHub web UI"
        echo ""
        echo "Manual steps:"
        echo "   1. Go to: https://github.com/$REPO/settings/secrets/actions"
        echo "   2. Click 'New repository secret'"
        echo "   3. Name: CODECOV_TOKEN"
        echo "   4. Value: $CODECOV_TOKEN"
        echo "   5. Click 'Add secret'"
    fi
else
    echo "📋 Manual Setup Required"
    echo ""
    echo "GitHub CLI not installed. Follow these steps:"
    echo ""
    echo "Step 1: Sign up for Codecov"
    echo "   1. Go to: $CODECOV_URL"
    echo "   2. Click 'Sign in with GitHub'"
    echo "   3. Authorize Codecov"
    echo "   4. Add repository: $REPO"
    echo "   5. Copy the token shown"
    echo ""
    echo "Step 2: Add Token to GitHub"
    echo "   1. Go to: https://github.com/$REPO/settings/secrets/actions"
    echo "   2. Click 'New repository secret'"
    echo "   3. Name: CODECOV_TOKEN"
    echo "   4. Value: [paste your token]"
    echo "   5. Click 'Add secret'"
    echo ""
    echo "✅ After adding the token, coverage will upload automatically!"
fi

echo ""
echo "📚 Full instructions: .github/CODECOV_SETUP.md"
echo ""

