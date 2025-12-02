# Deployment Scripts

This directory contains automation scripts for deploying the HelloblueGK platform.

## Scripts

- **`deploy-production.sh`** - Deploy to production environment
- **`deploy-to-render.sh`** - Deploy to Render cloud platform using Render CLI
- **`verify-deployment.sh`** - Verify deployment health and accessibility

## Usage

### Render Deployment
```bash
./deploy-to-render.sh
```
Requires Render CLI to be installed and authenticated. See deployment documentation for setup.

### Production Deployment
```bash
./deploy-production.sh
```

### Verify Deployment
```bash
./verify-deployment.sh [URL]
```

## Prerequisites

- Render CLI (for Render deployments)
- Docker (for containerized deployments)
- Git (for repository access)

## Related Documentation

- Deployment guides: `../../Docs/Deployment/`
- Docker configuration: `../../Docker/`
