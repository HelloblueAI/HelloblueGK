#!/bin/bash
# Branch Protection Setup Script for HelloblueGK
# Keeps GitHub branch protection aligned with current repo hygiene defaults.

set -euo pipefail

REPO="HelloblueAI/HelloblueGK"
BRANCH="main"

echo "Setting up branch protection rules for $REPO..."
echo ""

if ! command -v gh &> /dev/null; then
    echo "GitHub CLI (gh) is not installed."
    echo "   Install: https://cli.github.com/"
    exit 1
fi

if ! gh auth status &> /dev/null; then
    echo "Not authenticated with GitHub CLI."
    echo "   Run: gh auth login"
    exit 1
fi

echo "GitHub CLI is installed and authenticated"
echo ""
echo "Configuring branch protection rules..."

gh api "repos/$REPO/branches/$BRANCH/protection" \
  --method PUT \
  --input - <<'EOF'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "Build and Test",
      "Integration Tests",
      "Code Quality Checks",
      "Security Scan",
      "Analyze (csharp)"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": true,
    "require_last_push_approval": false,
    "required_approving_review_count": 0
  },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": true,
  "lock_branch": false,
  "allow_fork_syncing": false
}
EOF

echo ""
echo "Branch protection configured:"
echo "   - PRs required (code owner review)"
echo "   - Required checks: Build and Test, Integration Tests, Code Quality Checks, Security Scan, Analyze (csharp)"
echo "   - Branches must be up to date before merge"
echo "   - Linear history required (squash-only merges)"
echo "   - Conversation resolution required"
echo "   - Enforce for administrators"
echo "   - Force pushes and branch deletion blocked"
echo ""
echo "Also recommended (repo settings):"
echo "   - allow_squash_merge=true; allow_merge_commit=false; allow_rebase_merge=false"
echo "   - squash title=PR_TITLE; squash message=BLANK"
echo "   - delete_branch_on_merge=true; allow_auto_merge=true; allow_update_branch=true"
echo ""
echo "Branch protection is active."
