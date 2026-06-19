#!/usr/bin/env bash
# Zero-downtime rolling update for the local Docker Compose stack.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1
COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"
HAPROXY_CFG="${REPO_ROOT}/docker/haproxy/haproxy.cfg"

API_URL="${API_URL:-http://localhost:8089}"
WEB_URL="${WEB_URL:-http://localhost:3000}"
WAIT_TIMEOUT="${WAIT_TIMEOUT:-180}"

SKIP_TUNNEL_CHECK=false
ROLL_FLEET=false
PRUNE_CACHE=false

usage() {
  cat <<EOF
Usage: $(basename "$0") [--full] [--skip-tunnel-check] [--prune-cache]

Rebuild images and roll services one at a time for zero-downtime updates.
Compose: docker-compose.yml plus docker-compose.override.yml when present.

By default only API and web replicas are rolled (typical code changes).
Use --full to also roll storage nodes and dispatchers.
BuildKit layer and package caches are retained between runs unless --prune-cache is passed.

Options:
  --full                Also roll storage and dispatcher services
  --skip-tunnel-check   Skip Cloudflare tunnel API check when tunnel is running
  --prune-cache         Prune unused BuildKit cache and dangling images after success
  -h, --help            Show this help

Environment:
  WAIT_TIMEOUT          Seconds to wait per service health (default: 180)
  API_URL               Host URL for API LB check when ssh-lb is not running (default: http://localhost:8089)
  WEB_URL               Host URL for web LB check when ssh-lb is not running (default: http://localhost:3000)
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --full)
      ROLL_FLEET=true
      shift
      ;;
    --skip-tunnel-check)
      SKIP_TUNNEL_CHECK=true
      shift
      ;;
    --prune-cache)
      PRUNE_CACHE=true
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

FLEET_ROLL_SERVICES=(
  storage-1
  storage-2
  dispatcher-1
  dispatcher-2
)

APP_ROLL_SERVICES=(
  api-2
  api-1
  web-2
  web-1
)

FLEET_BUILD_SERVICES=(
  storage-1
  dispatcher-1
)

APP_BUILD_SERVICES=(
  api-1
  web-1
)

service_container() {
  case "$1" in
    storage-1) echo opengitbase_storage_1 ;;
    storage-2) echo opengitbase_storage_2 ;;
    dispatcher-1) echo opengitbase_dispatcher_1 ;;
    dispatcher-2) echo opengitbase_dispatcher_2 ;;
    api-1) echo opengitbase_api_1 ;;
    api-2) echo opengitbase_api_2 ;;
    web-1) echo opengitbase_web_1 ;;
    web-2) echo opengitbase_web_2 ;;
    *)
      echo "Unknown service: $1" >&2
      return 1
      ;;
  esac
}

FAILED_STEP=""

recovery_guidance() {
  cat >&2 <<'EOF'

Recovery (do not run 'docker-compose down' — healthy containers should still be serving traffic):
  1. Read logs for the failing service:
       docker-compose logs --tail=100 <service>
  2. Fix the underlying issue (build error, migration, health check, config).
  3. Re-run this script from the repo root:
       ./scripts/rolling-update.sh
  For fleet-layer changes only, include --full:
       ./scripts/rolling-update.sh --full
  For a full wipe and re-seed (destructive), use only when intentional:
       docker-compose down -v && docker-compose up -d --build
EOF
}

abort() {
  local step="$1"
  echo "" >&2
  echo "Rolling update failed at: ${step}" >&2
  recovery_guidance
  exit 1
}

run_step() {
  local name="$1"
  shift
  FAILED_STEP="${name}"
  echo "==> ${name}"
  if ! "$@"; then
    abort "${name}"
  fi
}

container_running() {
  local name="$1"
  docker inspect -f '{{.State.Running}}' "${name}" 2>/dev/null | grep -qx true
}

wait_for_healthy() {
  local service="$1"
  local container
  container="$(service_container "${service}")"
  local timeout="${WAIT_TIMEOUT}"
  local elapsed=0
  local status="starting"

  while [ "${elapsed}" -lt "${timeout}" ]; do
    if ! container_running "${container}"; then
      status="not running"
    else
      status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "${container}" 2>/dev/null || echo missing)"
      if [ "${status}" = "healthy" ]; then
        echo "    ${service}: healthy"
        return 0
      fi
      if [ "${status}" = "none" ]; then
        echo "    ${service}: running (no healthcheck)"
        return 0
      fi
    fi

    echo "    ${service}: ${status} (${elapsed}s / ${timeout}s)"
    sleep 3
    elapsed=$((elapsed + 3))
  done

  echo "    ${service}: timed out waiting for healthy (${timeout}s)" >&2
  compose logs --tail=40 "${service}" >&2 || true
  return 1
}

roll_service() {
  local service="$1"
  compose up -d --no-deps --remove-orphans "${service}"
  wait_for_healthy "${service}"
}

lb_http_check() {
  local url="$1"
  local host_fallback_url="$2"
  local attempt

  for attempt in 1 2 3 4 5 6 7 8 9 10; do
    if container_running opengitbase_ssh_lb; then
      local network
      network="$(docker inspect -f '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{end}}' opengitbase_ssh_lb)"
      if docker run --rm --network "${network}" curlimages/curl:8.5.0 -fsS "${url}" >/dev/null; then
        return 0
      fi
    elif curl -fsS "${host_fallback_url}" >/dev/null; then
      return 0
    fi

    sleep 3
  done

  return 1
}

verify_lb_api_health() {
  lb_http_check "http://api-lb:8080/health" "${API_URL}/health"
}

verify_lb_web() {
  lb_http_check "http://api-lb:8080/" "${WEB_URL}/"
}

echo "==> Zero-downtime rolling Docker Compose update"
echo "    Repo: ${REPO_ROOT}"
if [ "${ROLL_FLEET}" = true ]; then
  echo "    Mode: full (API, web, storage, dispatchers)"
else
  echo "    Mode: app (API and web only; pass --full for fleet)"
fi

run_step "Validate HAProxy configuration" \
  docker run --rm \
  -v "${HAPROXY_CFG}:/usr/local/etc/haproxy/haproxy.cfg:ro" \
  haproxy:3.2 \
  haproxy -c -f /usr/local/etc/haproxy/haproxy.cfg

run_step "Ensure postgres is up" \
  compose up -d --remove-orphans --wait-timeout "${WAIT_TIMEOUT}" --wait postgres

BUILD_SERVICES=("${APP_BUILD_SERVICES[@]}")
if [ "${ROLL_FLEET}" = true ]; then
  BUILD_SERVICES=("${FLEET_BUILD_SERVICES[@]}" "${APP_BUILD_SERVICES[@]}")
fi

run_step "Build service images" \
  compose build "${BUILD_SERVICES[@]}"

run_step "Run database migrations (api-migrate)" \
  compose --profile tools run --rm --no-deps --remove-orphans api-migrate

ROLL_SERVICES=("${APP_ROLL_SERVICES[@]}")
if [ "${ROLL_FLEET}" = true ]; then
  ROLL_SERVICES=("${FLEET_ROLL_SERVICES[@]}" "${APP_ROLL_SERVICES[@]}")
fi

for service in "${ROLL_SERVICES[@]}"; do
  run_step "Roll ${service}" roll_service "${service}"
done

run_step "Ensure load balancer (ssh-lb) is up" \
  compose up -d ssh-lb

run_step "Verify API health via load balancer" \
  verify_lb_api_health

run_step "Verify web UI via load balancer" \
  verify_lb_web

if [ "${SKIP_TUNNEL_CHECK}" = false ] && container_running opengitbase_cloudflare_tunnel; then
  run_step "Verify API via Cloudflare tunnel target (ssh-lb:8080)" \
    lb_http_check "http://ssh-lb:8080/health" "${API_URL}/health"
elif [ "${SKIP_TUNNEL_CHECK}" = true ]; then
  echo "==> Skipping Cloudflare tunnel API check (--skip-tunnel-check)"
else
  echo "==> Cloudflare tunnel not running; skipping tunnel API check"
fi

if [ "${PRUNE_CACHE}" = true ]; then
  run_step "Prune unused build cache and dangling images" \
    bash -c 'docker builder prune -f && docker image prune -f'
else
  echo "==> Skipping build cache prune (pass --prune-cache to reclaim disk space)"
fi

echo ""
echo "Rolling update completed successfully."
