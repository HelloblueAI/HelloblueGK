# Deployment Documentation

This directory contains all deployment-related documentation and guides for the HelloblueGK platform.

## Quick Start Guides

- **`DEPLOY_TO_RENDER.md`** - Deploying to Render, start here
- **`RENDER_DEPLOY_INSTRUCTIONS.md`** - Step-by-step Render walkthrough

## Platform-Specific Guides

### Render Cloud Platform
- **`DEPLOY_TO_RENDER.md`** - Complete Render deployment guide
- **`RENDER_DOCKER_SETUP.md`** - Docker setup for Render
- **`RENDER_DEPLOY_INSTRUCTIONS.md`** - Detailed Render instructions
- **`RENDER_DEPLOYMENT_CONFIG.txt`** - Configuration template

### General Deployment
- **`DEPLOYMENT_CHECKLIST.md`** - Pre-deployment checklist

## Related Resources

- **Deployment Scripts:** `../../Scripts/Deployment/`
- **Docker Configuration:** `../../Docker/`
- **Verification scope:** `../VERIFICATION_SCOPE.md`

## Deployment Options

1. **Render (Recommended)** - Cloud platform, 15-minute setup
2. **Docker** - Containerized deployment
3. **Systemd** - Linux service deployment (see WebAPI docs)
4. **Kubernetes** - Container orchestration (see `k8s-deployment.yaml` for a generic example)

Render is the supported path; see [DEPLOY_TO_RENDER.md](DEPLOY_TO_RENDER.md).
