# Connect HelloblueGK to PostgreSQL and JWT on Render (do it all)

Use this if your Render dashboard has **HelloblueGK** (Docker) and **hellobluegk-db** (PostgreSQL) and you want the app to use the DB and run without errors.

---

## 1. Get the database URL

1. In Render, open **hellobluegk-db** (your PostgreSQL service).
2. In **Connections**, copy **Internal Database URL** (looks like `postgresql://user:pass@hostname/dbname`).  
   Use **Internal** since the app is in the same region (Oregon).

---

## 2. Generate a JWT secret (one-time)

On your machine run:

```bash
openssl rand -base64 48
```

Copy the output (e.g. `K7xY2m...==`). This is your **JWT key** (32+ chars).

---

## 3. Set environment variables on HelloblueGK

1. In Render, open **HelloblueGK** (your Docker web service).
2. Go to **Environment** (left sidebar).
3. Add or edit these variables:

| Key | Value | Notes |
|-----|--------|--------|
| `DATABASE_URL` | *(paste the Internal Database URL from step 1)* | So the app uses hellobluegk-db |
| `Jwt__Key` | *(paste the output from step 2)* | Must be 32+ characters; use double quotes if it contains `+` or `=` |

4. Click **Save Changes**. Render will redeploy the service.

---

## 4. Optional: link the database (so Render injects DATABASE_URL)

If you prefer Render to inject the DB URL automatically:

1. In **HelloblueGK** → **Environment**, click **Add Environment Variable**.
2. Choose **Add from Render** or **Link Database** (wording may vary).
3. Select **hellobluegk-db** and add the variable name **DATABASE_URL**.
4. You still need to add **Jwt__Key** manually (step 3).

---

## 5. Check it worked

After the deploy finishes:

- **Logs:** In HelloblueGK → **Logs**, you should see the app start and no “SQL Server” or “Failed to initialize database” errors.
- **Health:** Open `https://<your-hellobluegk-url>.onrender.com/Health` (e.g. `https://hellobluegk.onrender.com/Health`).
- **Swagger:** Open `https://<your-hellobluegk-url>.onrender.com/swagger`.

If you still see DB errors, double-check that **DATABASE_URL** is set and is the **Internal** URL from hellobluegk-db.
