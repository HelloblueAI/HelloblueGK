#!/bin/bash
# Branch Protection Setup Script for HelloblueGK
# This script configures branch protection rules on GitHub

set -e

REPO="HelloblueAI/HelloblueGK"
BRANCH="main"

echo "🔒 Setting up branch protection rules for $REPO..."
echo ""

# Check if GitHub CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI (gh) is not installed."
    echo "   Please install it from: https://cli.github.com/"
    echo "   Or use the manual setup guide: Docs/internal/github-runbooks/BRANCH_PROTECTION_SETUP.md"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ Not authenticated with GitHub CLI."
    echo "   Please run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI is installed and authenticated"
echo ""

# Set branch protection rules
echo "📋 Configuring branch protection rules..."

gh api repos/$REPO/branches/$BRANCH/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["build","integration-tests","code-quality","security-scan"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true,"require_code_owner_reviews":false}' \
  --field restrictions=null \
  --field allow_force_pushes=false \
  --field allow_deletions=false

if [ $? -eq 0 ]; then
    echo "✅ Branch protection rules configured successfully!"
    echo ""
    echo "📊 Configured rules:"
    echo "   - Require pull request reviews (1 approval)"
    echo "   - Require status checks: build, integration-tests, code-quality, security-scan"
    echo "   - Require branches to be up to date"
    echo "   - Enforce rules for administrators"
    echo "   - Block force pushes"
    echo "   - Block branch deletion"
    echo ""
    echo "🎉 Branch protection is now active!"
else
    echo "❌ Failed to configure branch protection rules."
    echo "   You may need admin access to the repository."
    echo "   Check manual setup guide: Docs/internal/github-runbooks/BRANCH_PROTECTION_SETUP.md"
    exit 1
fi

