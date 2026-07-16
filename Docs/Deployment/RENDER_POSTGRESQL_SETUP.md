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

### Step 2: Get the Internal Database URL

After the database is created:

1. Open the PostgreSQL instance in the Render dashboard
2. Open **Connections**
3. Copy the **Internal Database URL** (starts with `postgresql://…`)

Prefer this URL over building a keyword-style connection string by hand.

### Step 3: Configure the Web Service (recommended)

HelloblueGK accepts Render’s URL directly:

1. Open your **HelloblueGK** web service → **Environment**
2. Add:

   | Key | Value |
   |-----|--------|
   | `DATABASE_URL` | *(paste Internal Database URL from Step 2 — Dashboard only)* |

3. Also set a strong `Jwt__Key` (32+ characters)
4. Save — Render redeploys automatically

The app converts `DATABASE_URL` to the provider connection string at runtime
(`WebAPI/Configuration/DatabaseConfiguration.cs`). **Do not commit the URL or
any password to git.**

### Step 4 (optional): Keyword-style connection string

If you must use `ConnectionStrings__DefaultConnection` instead of `DATABASE_URL`:

1. In the Dashboard, convert Render’s URL using a local scratch pad (not a repo file)
2. Set env var `ConnectionStrings__DefaultConnection` to that value
3. Never paste real host/user/password values into markdown, issues, or PRs

Keyword form uses host / port / database / user / password fields separated by
`;` — copy the mapping from [Npgsql connection string docs](https://www.npgsql.org/doc/connection-string-parameters.html),
not from this repository.

### Step 5: Verify Database Connection

After deployment completes:

1. Check service logs for database initialization messages
2. Hit `/Health`
3. Confirm login / API flows that need persistence

## Local development

| Mode | Configuration |
|------|----------------|
| SQLite (default in Development) | No env vars required (`Data Source=hellobluegk.db`) |
| Local PostgreSQL | Set `DATABASE_URL` or `ConnectionStrings__DefaultConnection` in your shell / user secrets — never in committed files |

## Troubleshooting

### Error: "Connection refused" / "timeout"
- Verify the database is running (Render dashboard)
- Free-tier DBs can sleep; first connect may take 30–60s
- Prefer **Internal** URL when the web service is in the same region

### Error: "Database already exists"
- Normal on redeploy — the app reuses the existing database

### Error: "Table does not exist"
- Check logs for `DatabaseInitializer` errors
- Tables are created from the EF Core model on first run (migrations when present)

## Security Best Practices

1. **Never commit connection strings or database URLs**
2. Use Dashboard / secret stores only (`DATABASE_URL`, `Jwt__Key`)
3. Separate databases for staging vs production
4. Rotate the database password in Render if it was ever pasted into chat, tickets, or git
5. Prefer Internal Database URL (no public network exposure)

## Next Steps

After database is set up:

1. Database URL configured in Dashboard
2. Tables created automatically on deploy
3. Auth and API persistence paths available

For production, consider backups (paid plans), monitoring, and connection pooling as load grows.
