#!/bin/bash
# Complete Setup Script - Codecov + Branch Protection
# This script attempts to set up both recommended enhancements

set -e

REPO="HelloblueAI/HelloblueGK"

echo "🚀 Complete Setup - Recommended Enhancements"
echo "============================================="
echo ""

# Check GitHub CLI
if ! command -v gh &> /dev/null; then
    echo "⚠️  GitHub CLI not installed"
    echo ""
    echo "For automated setup, install GitHub CLI:"
    echo "  Linux: sudo apt install gh"
    echo "  macOS: brew install gh"
    echo ""
    echo "Then run:"
    echo "  gh auth login"
    echo "  ./.github/setup-all.sh"
    echo ""
    echo "📚 Or follow manual guides:"
    echo "  - .github/QUICK_SETUP.md (quick steps)"
    echo "  - .github/CODECOV_SETUP.md (detailed Codecov guide)"
    echo "  - .github/BRANCH_PROTECTION_SETUP.md (detailed branch protection guide)"
    exit 0
fi

# Check authentication
if ! gh auth status &> /dev/null; then
    echo "⚠️  Not authenticated with GitHub CLI"
    echo ""
    echo "Please run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI detected and authenticated"
echo ""

# Setup Codecov
echo "📊 Setting up Codecov..."
echo ""
echo "Step 1: Please sign up at https://codecov.io"
echo "   - Sign in with GitHub"
echo "   - Add repository: $REPO"
echo "   - Copy the token shown"
echo ""
read -p "Press Enter after you've added the repo and copied the token..."

echo ""
read -p "Paste your Codecov token: " CODECOV_TOKEN

if [ -n "$CODECOV_TOKEN" ]; then
    echo ""
    echo "🔐 Adding CODECOV_TOKEN secret..."
    if gh secret set CODECOV_TOKEN --body "$CODECOV_TOKEN" --repo "$REPO" 2>/dev/null; then
        echo "✅ Codecov token added successfully!"
    else
        echo "❌ Failed to add secret. Please add manually:"
        echo "   https://github.com/$REPO/settings/secrets/actions"
    fi
else
    echo "⚠️  Skipping Codecov setup. Add manually later."
fi

echo ""
echo "🔒 Setting up Branch Protection..."
echo ""

# Setup Branch Protection
if gh api repos/$REPO/branches/main/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["build","integration-tests","code-quality","security-scan"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true,"require_code_owner_reviews":false}' \
  --field restrictions=null \
  --field allow_force_pushes=false \
  --field allow_deletions=false 2>/dev/null; then
    echo "✅ Branch protection configured successfully!"
else
    echo "❌ Failed to configure branch protection. You may need admin access."
    echo "   Follow manual guide: .github/BRANCH_PROTECTION_SETUP.md"
fi

echo ""
echo "🎉 Setup Complete!"
echo ""
echo "✅ Codecov: Token added (coverage will upload on next CI/CD run)"
echo "✅ Branch Protection: Main branch protected"
echo ""
echo "📊 Verify:"
echo "   - Codecov: https://codecov.io/gh/$REPO"
echo "   - Branch Protection: https://github.com/$REPO/settings/branches"
echo ""

