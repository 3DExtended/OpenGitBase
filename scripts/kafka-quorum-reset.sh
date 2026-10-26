#!/usr/bin/env bash
# Atomic lifecycle for the 3-broker KRaft cluster.
# Always start/stop all brokers together — never one-at-a-time recovery.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# shellcheck source=docker-env.sh
source "${SCRIPT_DIR}/docker-env.sh"
# shellcheck source=kafka-storage.sh
source "${SCRIPT_DIR}/kafka-storage.sh"

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
  --wipe          Remove broker containers, empty the Kafka data dirs (named volumes, or the
                  SSD cache pool bind mounts on Tower), then bootstrap fresh
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
# Compose volume keys (project prefix added by Docker). Tower bind-mounts the SSD cache pool
# instead (#232), so there these are only stale copies from before the move, if still present.
VOLUME_KEYS=(kafka1_data kafka2_data kafka3_data)
# Legacy names from the first durable deploy (safe to remove on wipe).
LEGACY_VOLUME_KEYS=(opengitbase_kafka1_data opengitbase_kafka2_data opengitbase_kafka3_data)

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

# The compose project as compose resolves it (COMPOSE_PROJECT_NAME, a top-level name: in the
# compose files, else the checkout directory); Compose v2 prints it first in `config`. Not read
# from containers: every checkout gives the brokers the same container_name, so a lookup by name
# can return another stack's project.
project_name() {
  local config name
  config="$(compose config)" || return 1
  name="$(awk '/^name: / && name == "" { name = $2 } END { print name }' <<<"${config}")"
  if [ -z "${name}" ]; then
    echo "error: compose config printed no top-level name: (Compose v2 required)" >&2
    return 1
  fi
  echo "${name}"
}

remove_kafka_volumes() {
  local project="${PROJECT}"
  echo "==> Removing Kafka data volumes for project '${project}'"
  local key full
  for key in "${VOLUME_KEYS[@]}" "${LEGACY_VOLUME_KEYS[@]}"; do
    full="${project}_${key}"
    if docker volume inspect "${full}" >/dev/null 2>&1; then
      docker volume rm -f "${full}" >/dev/null
      echo "    removed ${full}"
    else
      echo "    skip missing ${full}"
    fi
  done
}

prepare_volume_permissions() {
  echo "==> Ensuring Kafka data volumes are writable by uid 1000"
  compose run --rm --no-deps kafka-vol-init
}

republish_wakes() {
  if [ "${REPUBLISH_WAKES}" != true ]; then
    echo "==> Skipping wake republish (--no-republish)"
    return 0
  fi

  # Call API on the compose network (internal). Host curl to :8089 often hits HAProxy
  # with a non-internal client IP and gets 401/403 from InternalNetworkMiddleware.
  local network=""
  local container
  for container in opengitbase_api_1 opengitbase_ssh_lb opengitbase_kafka_1; do
    if docker inspect "${container}" >/dev/null 2>&1; then
      network="$(docker inspect -f '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{end}}' "${container}")"
      if [ -n "${network}" ]; then
        break
      fi
    fi
  done

  local path="/api/v1/internal/pipelines/kafka-wake-republish"
  echo "==> Republishing job wakes via in-network API${network:+ (network ${network})}"

  local ok=false
  if [ -n "${network}" ]; then
    if docker run --rm --network "${network}" curlimages/curl:8.5.0 \
      -fsS -X POST "http://api-1:8080${path}" >/dev/null 2>&1 \
      || docker run --rm --network "${network}" curlimages/curl:8.5.0 \
        -fsS -X POST "http://api-lb:8080${path}" >/dev/null 2>&1; then
      ok=true
    fi
  fi

  if [ "${ok}" != true ]; then
    local url="${API_URL%/}${path}"
    if curl -fsS -X POST "${url}" >/dev/null 2>&1; then
      ok=true
    fi
  fi

  if [ "${ok}" = true ]; then
    echo "    wake republish ok"
  else
    echo "    wake republish failed (API may lack the endpoint yet, or still starting); outbox + agent poll remain durable" >&2
  fi
  return 0
}

# A wipe resolves the project whose volumes it removes before it stops anything, so a failure
# leaves the running quorum alone.
PROJECT=""
if [ "${MODE}" = "wipe" ]; then
  PROJECT="$(project_name)" || {
    echo "error: could not resolve the compose project; nothing was stopped or removed" >&2
    exit 1
  }
fi

echo "==> Kafka quorum ${MODE} in ${REPO_ROOT}"

# Before touching the brokers: recreating them from compose files without Tower's SSD mounts
# would restart the quorum on the array (#232), and a wipe would empty the wrong dirs.
kafka_require_ssd_mounts

compose stop "${BROKERS[@]}" kafka-init kafka-vol-init 2>/dev/null || true
compose rm -f "${BROKERS[@]}" kafka-init kafka-vol-init 2>/dev/null || true

if [ "${MODE}" = "wipe" ]; then
  remove_kafka_volumes
  # Removing named volumes does not reach bind mounts; empty whatever kafka-vol-init mounts.
  echo "==> Emptying Kafka data dirs"
  kafka_empty_data_dirs
fi

# Create volumes (if needed) and chown before brokers start.
prepare_volume_permissions
compose up -d --no-deps --remove-orphans "${BROKERS[@]}"
wait_for_brokers_healthy
compose up -d --no-deps --remove-orphans kafka-init
compose logs --tail=30 kafka-init || true
republish_wakes

echo "==> Kafka quorum ${MODE} complete"
