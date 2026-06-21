#!/usr/bin/env bash
# Enable BuildKit without a .env file — prefix commands or source this script.
# Example:
#   DOCKER_BUILDKIT=1 COMPOSE_DOCKER_CLI_BUILD=1 docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d --build
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1

if [ -n "${BASH_SOURCE[0]:-}" ]; then
  _DOCKER_ENV_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  _DOCKER_ENV_ROOT="$(cd "${_DOCKER_ENV_DIR}/.." && pwd)"
  export GIT_SHA="$(git -C "${_DOCKER_ENV_ROOT}" rev-parse --short HEAD 2>/dev/null || echo unknown)"
fi
