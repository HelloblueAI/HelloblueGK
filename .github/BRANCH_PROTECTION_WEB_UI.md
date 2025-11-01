# 🔒 Branch Protection Setup - Web UI Guide

## Step-by-Step Instructions

### Step 1: Navigate to Branch Settings

**Open this URL:**
```
https://github.com/HelloblueAI/HelloblueGK/settings/branches
```

You should see a page titled "Branch protection rules"

---

### Step 2: Add New Rule

1. Look for the section **"Branch protection rules"**
2. Click the button **"Add rule"** (or **"Add branch protection rule"**)

---

### Step 3: Configure Branch Name

1. In the **"Branch name pattern"** field, type:
   ```
   main
   ```

---

### Step 4: Enable Protection Rules

Scroll down and enable these settings:

#### ✅ **Require a pull request before merging**

- Check the box: **"Require a pull request before merging"**
- Under this, set:
  - **Require approvals**: `1` (or use the dropdown to select 1)
  - ✅ Check: **"Dismiss stale pull request approvals when new commits are pushed"**
  - (Optional) Check: **"Require review from Code Owners"** if you want extra protection

#### ✅ **Require status checks to pass before merging**

- Check the box: **"Require status checks to pass before merging"**
- ✅ Check: **"Require branches to be up to date before merging"**
- In the **"Search for a status check"** box, search for and select these checks:
  - ✅ `build` (should appear as "Build and Test")
  - ✅ `integration-tests` (should appear as "Integration Tests")
  - ✅ `code-quality` (should appear as "Code Quality Checks")
  - ✅ `security-scan` (should appear as "Security Scan")

#### ✅ **Require conversation resolution before merging**

- Check the box: **"Require conversation resolution before merging"**

#### ✅ **Do not allow bypassing the above settings**

- Check the box: **"Do not allow bypassing the above settings"**
- ✅ Check: **"Include administrators"** (this ensures even admins follow the rules)

---

### Step 5: Save

1. Scroll to the bottom of the page
2. Click the green **"Create"** button (or **"Save changes"** if editing an existing rule)

---

### Step 6: Verify

After clicking "Create", you should see:
- ✅ A new rule listed under "Branch protection rules"
- ✅ The rule shows `main` as the branch pattern
- ✅ The enabled protections are listed

---

## ✅ What This Does

After setup:
- ✅ **No direct pushes** to `main` branch (must use Pull Requests)
- ✅ **Required code review** (at least 1 approval)
- ✅ **CI checks must pass** before merging
- ✅ **Protected from force pushes** and deletion
- ✅ **Even admins** must follow the rules

---

## 🧪 Test It

Try pushing directly to main (it should be blocked):
```bash
git push origin main
```

You should see an error like:
```
! [remote rejected] main -> main (protected branch hook declined)
```

This confirms branch protection is working! ✅

---

## 📚 Need Help?

- Detailed guide: `.github/BRANCH_PROTECTION_SETUP.md`
- Progress tracker: `.github/SETUP_PROGRESS.md`

---

**Time**: ~5 minutes  
**Difficulty**: Easy ⭐  
**Impact**: High 💯

