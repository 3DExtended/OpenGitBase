#!/usr/bin/env bash
# Production host (Unraid) compose environment — enables the Cloudflare tunnel profile.
# Usage:
#   source scripts/docker-env.production.sh
#   docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --build
# scripts/rolling-update.sh also enables this automatically when docker-compose.override.yml
# contains a configured opengitbase_cloudflare_tunnel token.
# shellcheck source=docker-env.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/docker-env.sh"
export COMPOSE_PROFILES=production-tunnel
