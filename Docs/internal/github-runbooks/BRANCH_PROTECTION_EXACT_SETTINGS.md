# 🔒 Branch Protection - Exact Settings to Configure

## Branch Name Pattern

**Enter exactly:**
```
main
```

---

## ✅ CHECK These Boxes (Enable):

### 1. ✅ **Protect matching branches**
   - Check this box at the top

### 2. ✅ **Require a pull request before merging**
   - Check this box
   - Set: **"Required number of approvals before merging"** = `1`
   - ✅ Check: **"Dismiss stale pull request approvals when new commits are pushed"**

### 3. ✅ **Require status checks to pass before merging**
   - Check this box
   - ✅ Check: **"Require branches to be up to date before merging"**
   - In the search box, search for and select these checks:
     - ✅ `build` (Build and Test)
     - ✅ `integration-tests` (Integration Tests)
     - ✅ `code-quality` (Code Quality Checks)
     - ✅ `security-scan` (Security Scan)

### 4. ✅ **Require conversation resolution before merging**
   - Check this box

### 5. ✅ **Do not allow bypassing the above settings**
   - Check this box
   - ✅ Check: **"Include administrators"** (under this section)

---

## ❌ DO NOT CHECK (Leave Unchecked):

- ❌ Require signed commits (optional, not needed)
- ❌ Require linear history (optional, not needed)
- ❌ Require merge queue (optional, not needed)
- ❌ Require deployments to succeed before merging (not configured)
- ❌ Lock branch (too restrictive - makes branch read-only)
- ❌ Restrict who can push to matching branches (too restrictive)
- ❌ Allow force pushes (we want to block this)
- ❌ Allow deletions (we want to block this)

---

## Summary

**Name:** `main`  
**Check:** Protect matching branches, Require PR, Require status checks, Require conversation resolution, Do not allow bypassing  
**Don't Check:** Everything else (leave unchecked)

---

## After Configuration

Click **"Create"** button at the bottom.

Your main branch will be protected! ✅

