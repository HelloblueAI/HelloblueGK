# Docker images

## Canonical production image

Use **`Docker/Dockerfile`** for Render, Railway, and CI.

```bash
docker build -f Docker/Dockerfile -t hellobluegk:local .
docker run --rm -p 8080:8080 -e PORT=8080 hellobluegk:local
```

- Non-root `appuser`
- Health check on `/Health`
- Binds via `PORT` (default `8080`)

`render.yaml` and `railway.json` point at this file.

## Compatibility shims

| File | Status |
|------|--------|
| `Docker/Dockerfile.render` | Same content as canonical (legacy path) |
| `Dockerfile.render` (repo root) | Deprecated; prefer `Docker/Dockerfile` |
| `Docker/Dockerfile.webapi`, `Dockerfile.production`, `Dockerfile.console` | Legacy variants; not CI-validated |
| `WebAPI/Dockerfile*` | Legacy; not used by Render/Railway |
| `PlasticityDemo/Dockerfile` | Demo-only |

## Dependabot

Docker base-image updates are tracked for `/Docker` (see `.github/dependabot.yml`).
