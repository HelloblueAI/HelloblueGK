#!/usr/bin/env bash
# Fallback build when BuildKit fails with "client version 1.43 too old".
# Uses the legacy builder (works when BuildKit's client/daemon version mismatch).
# Run from repo root. To fix permanently: upgrade Docker Desktop or docker-buildx.
set -e
cd "$(dirname "$0")/.."
export DOCKER_BUILDKIT=0
docker build -f Docker/Dockerfile -t hellobluegk:latest .
echo ""
echo "Image built. Run with a JWT key (required in production):"
echo "  docker run -p 8080:8080 -e Jwt__Key=\"your-secure-key-at-least-32-chars\" hellobluegk:latest"
