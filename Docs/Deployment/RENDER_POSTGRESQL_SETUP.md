# Render PostgreSQL Database Setup Guide

## Step-by-Step: Set Up PostgreSQL Database in Render

### Step 1: Create PostgreSQL Database in Render

1. **Go to Render Dashboard**
   - Visit: https://dashboard.render.com
   - Sign in to your account

2. **Create New PostgreSQL Database**
   - Click the **"New +"** button (top right)
   - Select **"PostgreSQL"**

3. **Configure Database**
   - **Name:** `hellobluegk-db` (or any name you prefer)
   - **Database:** `hellobluegk` (or leave default)
   - **User:** Leave default (auto-generated)
   - **Region:** Choose same region as your web service (Oregon for US West)
   - **PostgreSQL Version:** Latest (15 or 16)
   - **Plan:** 
     - **Free:** For testing (limited connections, may sleep)
     - **Starter ($7/month):** For production (always on, better performance)

4. **Click "Create Database"**
   - Wait 2-3 minutes for database to be created

### Step 2: Get Connection String

After database is created:

1. **Go to your PostgreSQL database** in Render dashboard
2. **Find "Connections" section**
3. **Copy the "Internal Database URL"** - it looks like:
   ```
   postgresql://hellobluegk_user:password@dpg-xxxxx-a.oregon-postgres.render.com/hellobluegk
   ```

### Step 3: Convert to .NET Connection String Format

Render provides a PostgreSQL URL, but .NET needs a specific format. Convert it:

**From Render format:**
```
postgresql://username:password@host:port/database
```

**To .NET format:**
```
Host=host;Port=port;Database=database;Username=username;Password=password
```

**Example conversion:**
- Render URL: `postgresql://hellobluegk_user:abc123@dpg-xxxxx-a.oregon-postgres.render.com:5432/hellobluegk`
- .NET format: `Host=dpg-xxxxx-a.oregon-postgres.render.com;Port=5432;Database=hellobluegk;Username=hellobluegk_user;Password=abc123`

### Step 4: Add Connection String to Web Service

1. **Go to your Web Service** (HelloblueGK)
2. **Click "Environment" tab**
3. **Add/Update this environment variable:**

   **Key:**
   ```
   ConnectionStrings__DefaultConnection
   ```

   **Value:**
   ```
   Host=your-host;Port=5432;Database=hellobluegk;Username=your-username;Password=your-password
   ```
   (Replace with your actual values from Step 3)

4. **Click "Save Changes"**
   - Render will automatically redeploy your service

### Step 5: Verify Database Connection

After deployment completes:

1. **Check Logs** in Render dashboard
   - Look for: `"Database and tables created successfully"`
   - If you see errors, check the connection string format

2. **Test Login Endpoint**
   ```bash
   curl -X POST https://hellobluegk.onrender.com/api/v1/Auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"test","password":"test"}'
   ```
   - Should return 401 (unauthorized) not 500 (server error)
   - 500 means database connection failed
   - 401 means database works, just no user exists yet

### Step 6: Create First User (Optional)

You can create a user via the register endpoint:

```bash
curl -X POST https://hellobluegk.onrender.com/api/v1/Auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "email": "admin@example.com",
    "password": "SecurePassword123!",
    "firstName": "Admin",
    "lastName": "User"
  }'
```

## Troubleshooting

### Error: "Database connection failed"
- Check connection string format (must use `Host=...;Port=...` format)
- Verify database is running (check Render dashboard)
- Check if database is sleeping (free tier) - it will wake up on first connection

### Error: "Database already exists"
- This is normal - means database was created successfully
- Application will use existing database

### Error: "Table does not exist"
- Database connection works but tables weren't created
- Check application logs for database initialization errors
- Tables are created automatically on first run via `EnsureCreated()`

### Database is Sleeping (Free Tier)
- Free tier databases sleep after 90 days of inactivity
- First request after sleep takes 30-60 seconds
- Consider upgrading to Starter plan for production

## Connection String Examples

### Render PostgreSQL (Production)
```
Host=dpg-xxxxx-a.oregon-postgres.render.com;Port=5432;Database=hellobluegk;Username=hellobluegk_user;Password=your-password
```

### Local Development (SQLite)
```
Data Source=hellobluegk.db
```

### Local Development (PostgreSQL)
```
Host=localhost;Port=5432;Database=hellobluegk;Username=postgres;Password=your-password
```

## Security Best Practices

1. **Never commit connection strings to git**
   - Always use environment variables
   - Render automatically keeps secrets secure

2. **Use different databases for dev/staging/prod**
   - Create separate PostgreSQL databases in Render
   - Use different environment variables per service

3. **Rotate passwords regularly**
   - Change database password in Render dashboard
   - Update environment variable immediately

4. **Use Internal Database URL for same-region services**
   - Faster and more secure
   - No external network exposure

## Next Steps

After database is set up:
1. ✅ Database connection configured
2. ✅ Tables created automatically
3. ✅ Login endpoint should work
4. ✅ Ready to create users and use the API

For production, consider:
- Setting up database backups (Render Pro plan)
- Monitoring database performance
- Setting up connection pooling if needed
