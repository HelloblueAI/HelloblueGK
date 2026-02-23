# Docker images

Build from **repository root**: `docker build -f Docker/<file> .`

## Run the WebAPI container

Production mode requires:
1. **JWT key** (at least 32 characters): `Jwt__Key`
2. **Database**: either set `ConnectionStrings__DefaultConnection` (e.g. SQLite) or `DATABASE_URL` (PostgreSQL on Render/Railway). If you omit both, the app will fail in production.

**Example: run with SQLite (no SQL Server needed; DB stored in a Docker volume)**

```bash
docker run -d -p 8080:8080 \
  -e Jwt__Key="your-secure-key-at-least-32-characters-long" \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/hellobluegk.db" \
  -v hellobluegk-data:/app/data \
  --name hellobluegk hellobluegk:latest
```

Then open http://localhost:8080/swagger and http://localhost:8080/Health.

| File | Purpose |
|------|--------|
| **Dockerfile** | WebAPI (production). Default image for CI and local runs. Port 8080. |
| **Dockerfile.render** | WebAPI for Render/Railway. Uses `PORT` from environment (default 5000). |
| **Dockerfile.production** | WebAPI with security hardening (non-root user, minimal layers). Port 8080. |
| **Dockerfile.console** | Console engine + PlasticityDemo only (no HTTP server). |

Root **Dockerfile.render** is a copy of **Docker/Dockerfile.render** for platforms that require the Dockerfile in the repo root.

## Fix "client version 1.43 too old" / "legacy builder deprecated"

**Option A – Use the fallback build (works immediately)**  
If BuildKit fails with "client version 1.43 is too old", use the legacy builder:

```bash
./Docker/build.sh
docker run -p 8080:8080 hellobluegk:latest
```

You may see a one-line deprecation warning; the image still builds. To get rid of it permanently, upgrade Docker (Option B).

**Option B – Use BuildKit (after upgrading Docker)**  
1. Upgrade **Docker Desktop** (or your `docker` and `docker-buildx` packages) so the client matches the daemon (API 1.44+).  
2. Run `./Docker/fix-buildkit.sh` so the default builder uses the host Docker driver.  
3. Build as usual: `docker build -f Docker/Dockerfile -t hellobluegk:latest .`
