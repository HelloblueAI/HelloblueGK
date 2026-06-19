# How to Trigger GitHub Actions Workflows

## Automatic Triggers

Workflows automatically run when you:
- Push to `main`, `develop`, or `production-deployment` branches
- Open a Pull Request targeting these branches
- Push to a configured `fix/**` branch

CodeQL is handled by GitHub default setup in repository security settings. Do not add or trigger a repository-owned `codeql.yml` workflow while default setup is enabled.

## Manual Trigger

If workflows don't start automatically, you can trigger them manually:

### Option 1: Via GitHub Web UI

1. **Go to Actions tab:**
   - Visit: https://github.com/HelloblueAI/HelloblueGK/actions

2. **Select workflow:**
   - Click on "CI/CD Pipeline" or "Daily Security Audit"

3. **Click "Run workflow":**
   - Select branch: `production-deployment`
   - Click "Run workflow" button

### Option 2: Make a Small Commit

```bash
# Make a small change to trigger workflows
git commit --allow-empty -m "Trigger CI/CD workflows"
git push origin production-deployment
```

### Option 3: Use GitHub CLI

```bash
# Install GitHub CLI if needed
# Linux: sudo apt install gh
# macOS: brew install gh

# Authenticate
gh auth login

# Trigger CI/CD workflow
gh workflow run ci.yml --ref production-deployment

# Trigger daily NuGet vulnerability audit
gh workflow run security-audit.yml --ref production-deployment
```

## Check Workflow Status

1. **GitHub Actions Tab:**
   - https://github.com/HelloblueAI/HelloblueGK/actions

2. **Check Latest Run:**
   - Look for the most recent workflow run
   - Should show "in progress" or "completed"

3. **View Logs:**
   - Click on the workflow run
   - Click on individual jobs to see logs

## ⚠️ Troubleshooting

### Workflows Not Starting?

1. **Check branch name:**
   - Workflows trigger on: `main`, `develop`, `production-deployment`
   - Make sure you're pushing to one of these

2. **Check workflow files exist:**
   - `.github/workflows/ci.yml` should exist
   - `.github/workflows/security-audit.yml` should exist

3. **Check GitHub Actions is enabled:**
   - Go to: Settings → Actions → General
   - Make sure "Allow all actions and reusable workflows" is enabled

4. **Manual trigger:**
   - Use Option 1 or 2 above to trigger manually

### Workflows Failing?

1. **Check logs:**
   - Click on the failed workflow
   - Check which job failed
   - Read the error message

2. **Common issues:**
   - Missing dependencies
   - Build errors
   - Test failures
   - Configuration issues

## Current Status

After pushing to `production-deployment`:
- Workflows should trigger automatically
- CI/CD Pipeline will run
- Daily Security Audit remains available for scheduled or manual NuGet vulnerability scans
- CodeQL results are managed by GitHub default setup

---

**Workflows are configured to trigger on `production-deployment` and the other configured branches.**

