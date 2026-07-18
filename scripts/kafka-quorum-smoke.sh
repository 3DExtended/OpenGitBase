#!/usr/bin/env bash
# Smoke: Kafka quorum survives an atomic restart (durable volumes).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# shellcheck source=docker-env.sh
source "${SCRIPT_DIR}/docker-env.sh"

COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"
COMPOSE_ARGS=(-f "${COMPOSE_FILE}")
if [ -f "${OVERRIDE_FILE}" ]; then
  COMPOSE_ARGS+=(-f "${OVERRIDE_FILE}")
fi

compose() {
  docker-compose "${COMPOSE_ARGS[@]}" "$@"
}

echo "==> Ensuring Kafka brokers are up"
compose up -d --no-deps kafka-1 kafka-2 kafka-3
"${SCRIPT_DIR}/kafka-quorum-reset.sh" --restart --no-republish

echo "==> Verifying cluster API versions"
compose exec -T kafka-1 \
  /opt/kafka/bin/kafka-broker-api-versions.sh \
  --bootstrap-server kafka-1:29092,kafka-2:29092,kafka-3:29092 \
  >/dev/null

echo "==> Kafka quorum smoke passed"
