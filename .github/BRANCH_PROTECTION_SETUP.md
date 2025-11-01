# Branch Protection Rules Setup Guide

This document provides instructions for configuring branch protection rules on GitHub to require CI checks before merging.

## Automated Setup (Using GitHub CLI)

If you have GitHub CLI installed, run:

```bash
gh api repos/HelloblueAI/HelloblueGK/branches/main/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["build","integration-tests","code-quality","security-scan"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":1,"dismiss_stale_reviews":true,"require_code_owner_reviews":false}' \
  --field restrictions=null
```

## Manual Setup (GitHub Web UI)

### Step 1: Navigate to Branch Protection Settings

1. Go to: `https://github.com/HelloblueAI/HelloblueGK`
2. Click on **Settings** (requires admin access)
3. Click on **Branches** in the left sidebar
4. Click **Add rule** next to "Branch protection rules"
5. Enter `main` in the "Branch name pattern" field

### Step 2: Configure Protection Rules

Enable the following settings:

#### ✅ **Protect matching branches**

- [x] **Require a pull request before merging**
  - [x] Require approvals: **1**
  - [x] Dismiss stale pull request approvals when new commits are pushed
  - [x] Require review from Code Owners (optional)

- [x] **Require status checks to pass before merging**
  - [x] Require branches to be up to date before merging
  - [x] Select the following required status checks:
    - ✅ `build` (Build and Test)
    - ✅ `integration-tests` (Integration Tests)
    - ✅ `code-quality` (Code Quality Checks)
    - ✅ `security-scan` (Security Scan)

- [x] **Require conversation resolution before merging**

- [x] **Do not allow bypassing the above settings**
  - [x] Include administrators

- [x] **Restrict who can push to matching branches**
  - (Optional) Restrict to specific teams/users

#### ✅ **Rules applied to everyone including administrators**

### Step 3: Save and Verify

1. Click **Create** or **Save changes**
2. Verify by creating a test PR - it should require CI checks to pass
3. The main branch should now be protected

## Recommended Additional Settings

### Required Status Checks

Ensure these checks are required:
- `build` - Build and Test job
- `integration-tests` - Integration Tests job  
- `code-quality` - Code Quality Checks job
- `security-scan` - Security Scan job
- `codeql` - CodeQL Analysis (if enabled)

### Pull Request Requirements

- **Minimum reviews**: 1
- **Require approving reviews**: Yes
- **Dismiss stale reviews**: Yes
- **Require review from Code Owners**: Optional (enable if you have CODEOWNERS file)

### Additional Protections

- **Require linear history**: Optional (keeps git history clean)
- **Require signed commits**: Optional (requires GPG signing)
- **Require branch to be up to date**: Recommended (ensures latest changes)

## Verification

After setup, verify protection by:

1. Creating a test branch
2. Making a change
3. Opening a pull request
4. Confirming that:
   - CI checks must pass before merge
   - At least 1 approval is required
   - Direct pushes to main are blocked

## Notes

- Branch protection rules require **admin access** to the repository
- These settings help maintain code quality and prevent broken code from being merged
- CI/CD pipeline will automatically run on all pull requests
- Failed CI checks will block merging until fixed

---

**Status**: ✅ Ready to configure  
**Last Updated**: 2025  
**Repository**: HelloblueAI/HelloblueGK

