# 🔐 GitHub CLI Authentication Guide

## Quick Steps

1. **Choose HTTPS** (recommended - easier to use)
2. **Choose "Login with a web browser"** (easiest method)
3. **Copy the one-time code** shown
4. **Press Enter** to open browser
5. **Paste code** in browser and authorize
6. **Done!** ✅

## After Authentication

Verify authentication:
```bash
gh auth status
```

You should see:
```
✓ Logged in to github.com as <your-username>
```

## Then Run Setup

Once authenticated, run:
```bash
# Complete setup (Codecov + Branch Protection)
./.github/setup-all.sh
```

---

**Tip**: If browser doesn't open automatically, manually visit the URL shown and paste the code.

