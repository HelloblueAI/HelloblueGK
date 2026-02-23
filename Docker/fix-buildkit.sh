#!/usr/bin/env bash
# Use the host Docker builder so BuildKit works without "legacy builder deprecated"
# or "client version 1.43 too old" errors. Only one 'docker' driver is allowed;
# this script finds it and switches to it. Run from repo root.
set -e
# Find the builder that uses the host Docker driver (only one can exist)
BUILDER=$(docker buildx ls 2>/dev/null | awk 'NR>1 && $3 == "docker" { print $1; exit }')
if [[ -z "$BUILDER" ]]; then
  echo "No builder with driver 'docker' found."
  echo "Try: docker buildx use default"
  exit 1
fi
echo "Using builder: $BUILDER"
docker buildx use "$BUILDER"
echo "Done. Use: docker build -f Docker/Dockerfile -t hellobluegk:latest ."
