# Codecov Integration Setup Guide

This guide will help you set up Codecov integration for automatic code coverage reporting.

## Quick Setup (5 minutes)

### Step 1: Sign up for Codecov

1. Go to [codecov.io](https://codecov.io)
2. Click **Sign in with GitHub**
3. Authorize Codecov to access your GitHub account
4. Select the **HelloblueAI** organization (or your personal account)
5. Find **HelloblueGK** repository and click **Add repo**

### Step 2: Get Your Codecov Token

1. After adding the repository, Codecov will show you a token
2. **Copy this token** - you'll need it for Step 3

### Step 3: Add Token to GitHub Secrets

1. Go to your repository: `https://github.com/HelloblueAI/HelloblueGK`
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Name: `CODECOV_TOKEN`
5. Value: Paste your Codecov token
6. Click **Add secret**

### Step 4: Verify Configuration

The CI/CD pipeline is already configured to use Codecov. After your next push:

1. The CI pipeline will run tests with coverage collection
2. Coverage reports will be uploaded to Codecov automatically
3. You'll see coverage badges and reports on Codecov dashboard

## Configuration Details

### Current Setup

- **Coverage Target**: 95% (project-level)
- **Patch Coverage**: 90% minimum
- **Coverage Format**: Cobertura XML
- **Badge**: Automatically generated

### Coverage Thresholds

- **Project Coverage**: 95% target, 1% threshold
- **Patch Coverage**: 90% target, 1% threshold
- **Carryforward**: Enabled for both unit and integration tests

### File Configuration

The `codecov.yml` file in the repository root contains:

```yaml
coverage:
  precision: 2
  round: down
  range: "70...100"
  
  status:
    project:
      default:
        target: 95%
        threshold: 1%
    patch:
      default:
        target: 90%
        threshold: 1%
```

## Verification

After setup, verify everything works:

1. **Check CI/CD Pipeline**:
   - Go to Actions tab
   - Check that coverage collection runs successfully
   - Verify Codecov upload step completes

2. **Check Codecov Dashboard**:
   - Visit: `https://codecov.io/gh/HelloblueAI/HelloblueGK`
   - You should see coverage reports and trends

3. **Check PR Comments**:
   - Create a test PR
   - Codecov should comment with coverage changes

## Troubleshooting

### Issue: Coverage not uploading

**Solution**: 
- Verify `CODECOV_TOKEN` secret is set correctly
- Check CI/CD logs for upload errors
- Ensure `coverlet.collector` package is installed (already configured)

### Issue: Coverage percentage seems wrong

**Solution**:
- Check `codecov.yml` configuration
- Verify test projects are included in coverage collection
- Check that coverage files are generated in correct format

### Issue: Badge not showing

**Solution**:
- Wait a few minutes after first upload
- Check Codecov dashboard for badge settings
- Verify repository is public or has proper access

## Benefits

✅ **Automatic Coverage Tracking**: Every PR shows coverage changes  
✅ **Coverage Badges**: Visual indicators in README  
✅ **Historical Trends**: Track coverage over time  
✅ **PR Comments**: Automatic comments on coverage changes  
✅ **Quality Gates**: Enforce minimum coverage thresholds  

## Next Steps

1. ✅ Sign up for Codecov (if not done)
2. ✅ Add repository to Codecov
3. ✅ Set up GitHub secret
4. ✅ Push a commit to trigger CI/CD
5. ✅ Verify coverage reports appear

---

**Status**: ✅ Ready to configure  
**Estimated Time**: 5 minutes  
**Repository**: HelloblueAI/HelloblueGK

