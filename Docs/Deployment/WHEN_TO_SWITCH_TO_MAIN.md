# When to Switch Render Service to Main Branch

## Current Situation

- **Current Branch:** `fix/codeql-suppressions`
- **PR Status:** Created and loading
- **Render Service:** Currently deploying from `fix/codeql-suppressions`

## When to Switch to Main Branch

### ✅ Switch to Main AFTER:

1. **PR is Merged** ✅
   - Wait for PR review and approval
   - Merge PR to `main` branch
   - Verify merge completed successfully

2. **Main Branch Has All Your Changes** ✅
   - All bug fixes are in `main`
   - PostgreSQL support is in `main`
   - All environment variables are documented

3. **You're Ready for Production** ✅
   - Database is set up
   - Environment variables are configured
   - You want stable production deployment

### ⏰ Timeline

**Option 1: Switch Now (Before PR Merge)**
- ✅ Keep using `fix/codeql-suppressions` branch
- ✅ Test everything works
- ⚠️ Switch to `main` after PR is merged

**Option 2: Wait for PR Merge (Recommended)**
- ✅ Let PR be reviewed and merged
- ✅ Switch to `main` branch in Render
- ✅ More stable for production

## How to Switch Render Service to Main Branch

### Step 1: Wait for PR Merge

1. **Check PR Status**
   - Go to: https://github.com/HelloblueAI/HelloblueGK/pulls
   - Find your PR
   - Wait for it to be merged

2. **Verify Merge**
   - Check that `main` branch has your commits
   - All changes should be in `main`

### Step 2: Update Render Service

1. **Go to Render Dashboard**
   - Visit: https://dashboard.render.com
   - Select your service: **HelloblueGK**

2. **Change Branch**
   - Go to **"Settings"** tab
   - Find **"Build & Deploy"** section
   - Click **"Edit"** next to **"Branch"**
   - Change from: `fix/codeql-suppressions`
   - Change to: `main`
   - Click **"Save Changes"**

3. **Render Will Auto-Deploy**
   - Render automatically detects branch change
   - Starts new deployment from `main` branch
   - Wait 5-10 minutes for deployment

### Step 3: Verify Deployment

1. **Check Deployment Logs**
   - Watch build logs in Render
   - Verify build succeeds
   - Check for any errors

2. **Test Endpoints**
   ```bash
   # Health check
   curl https://hellobluegk.onrender.com/Health
   
   # Login endpoint (should work with database)
   curl -X POST https://hellobluegk.onrender.com/api/v1/Auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"test","password":"test"}'
   ```

## Current Recommendation

### For Now (Before PR Merge):
- ✅ **Keep using `fix/codeql-suppressions` branch**
- ✅ Test everything works
- ✅ Set up PostgreSQL database
- ✅ Configure all environment variables

### After PR Merge:
- ✅ **Switch to `main` branch in Render**
- ✅ More stable for production
- ✅ Follows best practices
- ✅ Easier to track deployments

## Environment Variables to Keep

When you switch to `main`, make sure these are still set:

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:$PORT
Jwt__Key=your-secure-key
Jwt__Issuer=hellobluegk
Jwt__Audience=hellobluegk-api
ConnectionStrings__DefaultConnection=your-postgresql-connection-string
```

## Summary

**Timeline:**
1. ✅ **Now:** Use `fix/codeql-suppressions` branch, test everything
2. ⏳ **Wait:** For PR to be reviewed and merged
3. ✅ **After Merge:** Switch Render service to `main` branch
4. ✅ **Production:** Stable deployment from `main`

**You can switch to `main` anytime after your PR is merged!**
