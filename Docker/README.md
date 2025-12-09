# Docker Configuration

This directory contains all Docker-related configuration files for the HelloblueGK project.

## Dockerfiles

- **`Dockerfile.render`** - Production Dockerfile optimized for Render cloud deployment
  - Used by: Render auto-deployment via `render.yaml`
  - Configuration: See `render.yaml` in project root

- **`Dockerfile`** - Main production Dockerfile
  - Standard Docker build configuration

- **`Dockerfile.production`** - Alternative production configuration
  - Advanced security and optimization features

- **`Dockerfile.webapi`** - WebAPI-specific Dockerfile
  - Focused on WebAPI service only

- **`Dockerfile.backup`** - Backup/archive Dockerfile
  - Kept for reference

## Usage

### Render Deployment
The `Dockerfile.render` is automatically used when deploying via Render (configured in `render.yaml`).

### Manual Docker Build
```bash
# Build from root directory
docker build -f Docker/Dockerfile.render -t hellobluegk:latest .
```

## Related Documentation
- Deployment guides: `../Docs/Deployment/`
- Technical docs: `../Docs/Technical/ENTERPRISE_DEPLOYMENT.md`
