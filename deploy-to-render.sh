#!/bin/bash

# Render CLI Deployment Script
# Make sure you've run 'render config init' first to authenticate!

set -e

export PATH="$HOME/.local/bin:$PATH"

# Generate unique name
UNIQUE_NAME="hellobluegk$(date +%s)"

echo "🚀 Deploying to Render..."
echo "Service name: $UNIQUE_NAME"
echo ""

render services create web \
  --name "$UNIQUE_NAME" \
  --repo https://github.com/HelloblueAI/HelloblueGK \
  --branch main \
  --dockerfile-path Dockerfile.render \
  --region oregon

echo ""
echo "✅ Deployment initiated!"
echo "Check status at: https://dashboard.render.com"

