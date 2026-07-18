#!/usr/bin/env bash
# Atomic lifecycle for the 3-broker KRaft cluster.
# Always start/stop all brokers together — never one-at-a-time recovery.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# shellcheck source=docker-env.sh
source "${SCRIPT_DIR}/docker-env.sh"

COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"
WAIT_TIMEOUT="${WAIT_TIMEOUT:-300}"
API_URL="${API_URL:-http://localhost:8089}"
MODE="restart"
REPUBLISH_WAKES=true

usage() {
  cat <<EOF
Usage: $(basename "$0") [--restart|--wipe] [--no-republish] [--help]

Manage the OpenGitBase Kafka quorum as one unit.

  --restart       Stop and start kafka-1/2/3 together (default; keeps volumes)
  --wipe          Remove broker containers AND named volumes, then bootstrap fresh
  --no-republish  Skip POST /api/v1/internal/pipelines/kafka-wake-republish after init
  -h, --help      Show this help

Environment:
  WAIT_TIMEOUT   Seconds to wait for all brokers healthy (default: 300)
  API_URL        Base URL for wake republish (default: http://localhost:8089)
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --restart)
      MODE="restart"
      shift
      ;;
    --wipe)
      MODE="wipe"
      shift
      ;;
    --no-republish)
      REPUBLISH_WAKES=false
      shift
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

COMPOSE_ARGS=(-f "${COMPOSE_FILE}")
if [ -f "${OVERRIDE_FILE}" ]; then
  COMPOSE_ARGS+=(-f "${OVERRIDE_FILE}")
fi

compose() {
  docker-compose "${COMPOSE_ARGS[@]}" "$@"
}

BROKERS=(kafka-1 kafka-2 kafka-3)
VOLUMES=(opengitbase_kafka1_data opengitbase_kafka2_data opengitbase_kafka3_data)

broker_health() {
  local service="$1"
  local container
  container="$(compose ps -q "${service}" 2>/dev/null || true)"
  if [ -z "${container}" ]; then
    echo "missing"
    return
  fi
  docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "${container}" 2>/dev/null || echo "missing"
}

wait_for_brokers_healthy() {
  local elapsed=0
  echo "==> Waiting for Kafka brokers to become healthy (timeout ${WAIT_TIMEOUT}s)"
  while [ "${elapsed}" -lt "${WAIT_TIMEOUT}" ]; do
    local all_healthy=true
    local status
    for service in "${BROKERS[@]}"; do
      status="$(broker_health "${service}")"
      echo "    ${service}: ${status} (${elapsed}s / ${WAIT_TIMEOUT}s)"
      if [ "${status}" != "healthy" ]; then
        all_healthy=false
      fi
    done
    if [ "${all_healthy}" = true ]; then
      echo "==> All Kafka brokers healthy"
      return 0
    fi
    sleep 5
    elapsed=$((elapsed + 5))
  done
  echo "Kafka brokers did not become healthy within ${WAIT_TIMEOUT}s" >&2
  for service in "${BROKERS[@]}"; do
    compose logs --tail=40 "${service}" >&2 || true
  done
  return 1
}

project_name() {
  # Prefer compose project name from a running broker, else directory name.
  local container
  container="$(compose ps -q kafka-1 2>/dev/null || true)"
  if [ -n "${container}" ]; then
    docker inspect -f '{{index .Config.Labels "com.docker.compose.project"}}' "${container}" 2>/dev/null && return
  fi
  basename "${REPO_ROOT}" | tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9_-'
}

remove_kafka_volumes() {
  local project
  project="$(project_name)"
  echo "==> Removing Kafka data volumes for project '${project}'"
  for vol in "${VOLUMES[@]}"; do
    local full="${project}_${vol}"
    if docker volume inspect "${full}" >/dev/null 2>&1; then
      docker volume rm -f "${full}" >/dev/null
      echo "    removed ${full}"
    else
      echo "    skip missing ${full}"
    fi
  done
}

republish_wakes() {
  if [ "${REPUBLISH_WAKES}" != true ]; then
    echo "==> Skipping wake republish (--no-republish)"
    return 0
  fi

  local url="${API_URL%/}/api/v1/internal/pipelines/kafka-wake-republish"
  echo "==> Republishing job wakes via ${url}"
  if curl -fsS -X POST "${url}" >/dev/null; then
    echo "    wake republish ok"
  else
    echo "    wake republish failed (API may still be starting); outbox + agent poll remain durable" >&2
    return 0
  fi
}

echo "==> Kafka quorum ${MODE} in ${REPO_ROOT}"

compose stop "${BROKERS[@]}" kafka-init 2>/dev/null || true
compose rm -f "${BROKERS[@]}" kafka-init 2>/dev/null || true

if [ "${MODE}" = "wipe" ]; then
  remove_kafka_volumes
fi

compose up -d --no-deps --remove-orphans "${BROKERS[@]}"
wait_for_brokers_healthy
compose up -d --no-deps --remove-orphans kafka-init
# kafka-init exits after creating topics
compose logs --tail=30 kafka-init || true
republish_wakes

echo "==> Kafka quorum ${MODE} complete"
