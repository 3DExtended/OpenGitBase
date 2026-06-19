#!/usr/bin/env bash
# Enable BuildKit without a .env file — prefix commands or source this script.
# Example:
#   DOCKER_BUILDKIT=1 COMPOSE_DOCKER_CLI_BUILD=1 docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d --build
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1
