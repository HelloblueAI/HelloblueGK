# 🔒 Branch Protection Setup - Classic Rule

## Which Button to Click?

You'll see two options:
- **"Add branch ruleset"** ← Skip this (newer feature, more complex)
- **"Add classic branch protection rule"** ← **Click this one!** ✅

---

## Step-by-Step: Classic Branch Protection

### Step 1: Click "Add classic branch protection rule"

Click the button that says **"Add classic branch protection rule"**

---

### Step 2: Branch Name Pattern

In the **"Branch name pattern"** field, type:
```
main
```

---

### Step 3: Enable Protection Settings

Scroll down and check these boxes:

#### ✅ **Require a pull request before merging**

- ☑️ Check: **"Require a pull request before merging"**
- Set **"Required number of approvals before merging"**: `1`
- ☑️ Check: **"Dismiss stale pull request approvals when new commits are pushed"**

#### ✅ **Require status checks to pass before merging**

- ☑️ Check: **"Require status checks to pass before merging"**
- ☑️ Check: **"Require branches to be up to date before merging"**

Then, in the **"Search for a status check"** box, search for and select:
- ☑️ `build` (Build and Test)
- ☑️ `integration-tests` (Integration Tests)
- ☑️ `code-quality` (Code Quality Checks)
- ☑️ `security-scan` (Security Scan)

#### ✅ **Require conversation resolution before merging**

- ☑️ Check: **"Require conversation resolution before merging"**

#### ✅ **Do not allow bypassing the above settings**

- ☑️ Check: **"Do not allow bypassing the above settings"**
- ☑️ Check: **"Include administrators"**

---

### Step 4: Create Rule

1. Scroll to the bottom
2. Click the green **"Create"** button

---

## ✅ Done!

After clicking "Create", you'll see:
- ✅ A new rule for `main` branch
- ✅ All protections enabled
- ✅ Main branch is now protected!

---

## 🧪 Verify It Works

Try pushing directly to main (should be blocked):
```bash
git push origin main
```

If you see an error like "protected branch hook declined", it's working! ✅

---

**Quick Summary**: Click **"Add classic branch protection rule"** → Enter `main` → Enable settings → Create ✅

