#!/usr/bin/env bash
# Zero-downtime rolling update for the local Docker Compose stack.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# shellcheck source=docker-env.sh
source "${SCRIPT_DIR}/docker-env.sh"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"
HAPROXY_CFG="${REPO_ROOT}/docker/haproxy/haproxy.cfg"

API_URL="${API_URL:-http://localhost:8089}"
WEB_URL="${WEB_URL:-http://localhost:3000}"
WAIT_TIMEOUT="${WAIT_TIMEOUT:-180}"

SKIP_TUNNEL_CHECK=false
ROLL_FLEET=false
PRUNE_CACHE=false
ROLL_KAFKA=false

usage() {
  cat <<EOF
Usage: $(basename "$0") [--full] [--kafka] [--skip-tunnel-check] [--prune-cache]

Rebuild images and roll services one at a time for zero-downtime updates.
Compose: docker-compose.yml plus docker-compose.override.yml when present.

By default only API and web replicas are rolled (typical code changes).
Use --full to also roll storage nodes and dispatchers.
Kafka is left alone unless --kafka is passed (atomic restart via kafka-quorum-reset.sh).
BuildKit layer and package caches are retained between runs unless --prune-cache is passed.

Options:
  --full                Also roll storage and dispatcher services
  --kafka               After app roll, atomically restart the Kafka quorum (keeps volumes)
  --skip-tunnel-check   Skip Cloudflare tunnel API check when tunnel is running
  --prune-cache         Prune unused BuildKit cache and dangling images after success
  -h, --help            Show this help

Environment:
  WAIT_TIMEOUT          Seconds to wait per service health (default: 180)
  API_URL               Host URL for API LB check when ssh-lb is not running (default: http://localhost:8089)
  WEB_URL               Host URL for web LB check when ssh-lb is not running (default: http://localhost:3000)
  COMPOSE_PROFILES      Compose profiles to enable (production sets production-tunnel automatically when configured)
  OPENGITBASE_ENABLE_TUNNEL  Set to 1 to force the production-tunnel profile
EOF
}

enable_production_tunnel_profile() {
  if [ -n "${COMPOSE_PROFILES:-}" ]; then
    return
  fi

  if [ "${OPENGITBASE_ENABLE_TUNNEL:-0}" = "1" ]; then
    export COMPOSE_PROFILES=production-tunnel
    return
  fi

  if [ ! -f "${OVERRIDE_FILE}" ]; then
    return
  fi

  if ! grep -q 'opengitbase_cloudflare_tunnel:' "${OVERRIDE_FILE}"; then
    return
  fi

  if grep -q 'REPLACE_WITH_CLOUDFLARE_TUNNEL_TOKEN' "${OVERRIDE_FILE}"; then
    return
  fi

  export COMPOSE_PROFILES=production-tunnel
}

production_tunnel_profile_enabled() {
  if [ -z "${COMPOSE_PROFILES:-}" ]; then
    return 1
  fi

  case ",${COMPOSE_PROFILES}," in
    *,production-tunnel,*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

while [ $# -gt 0 ]; do
  case "$1" in
    --full)
      ROLL_FLEET=true
      shift
      ;;
    --kafka)
      ROLL_KAFKA=true
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

enable_production_tunnel_profile

compose() {
  docker-compose "${COMPOSE_ARGS[@]}" "$@"
}

prepare_deploy_build_env() {
  export GIT_SHA="$(git -C "${REPO_ROOT}" rev-parse --short HEAD 2>/dev/null || echo unknown)"

  local bump_enabled=false
  if [ -f "${OVERRIDE_FILE}" ] && grep -Eq 'DEPLOY_BUMP:[[:space:]]*["'"'"']?1["'"'"']?' "${OVERRIDE_FILE}"; then
    bump_enabled=true
  fi
  if [ "${DEPLOY_BUMP:-0}" = "1" ]; then
    bump_enabled=true
  fi

  if [ "${bump_enabled}" = true ]; then
    export DEPLOY_BUMP=1
    bash "${SCRIPT_DIR}/docker/bump-deploy-patch.sh"
  elif [ -d "${REPO_ROOT}/.deploy-patch" ]; then
    rm -rf "${REPO_ROOT}/.deploy-patch"
  fi
}

STORAGE_ROLL_SERVICES=(
  storage-1
  storage-2
  storage-3
)

DISPATCHER_ROLL_SERVICES=(
  dispatcher-1
  dispatcher-2
)

APP_ROLL_SERVICES=(
  api-2
  api-1
)

WEB_ROLL_SERVICES=(
  web-1
  web-2
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
    storage-3) echo opengitbase_storage_3 ;;
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

format_elapsed() {
  local total="${1}"
  local hours=$((total / 3600))
  local minutes=$(((total % 3600) / 60))
  local seconds=$((total % 60))

  if [ "${hours}" -gt 0 ]; then
    printf '%dh %dm %ds' "${hours}" "${minutes}" "${seconds}"
  elif [ "${minutes}" -gt 0 ]; then
    printf '%dm %ds' "${minutes}" "${seconds}"
  else
    printf '%ds' "${seconds}"
  fi
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

roll_web_replicas() {
  compose up -d --no-deps --remove-orphans web-1 web-2
  wait_for_healthy web-1
  wait_for_healthy web-2
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

SECONDS=0

echo "==> Zero-downtime rolling Docker Compose update"
echo "    Repo: ${REPO_ROOT}"
if [ "${ROLL_FLEET}" = true ]; then
  echo "    Mode: full (API, web, storage, dispatchers)"
else
  echo "    Mode: app (API and web only; pass --full for fleet)"
fi
if production_tunnel_profile_enabled; then
  echo "    Cloudflare tunnel: enabled (COMPOSE_PROFILES=${COMPOSE_PROFILES})"
fi

run_step "Validate HAProxy configuration" \
  docker run --rm \
  -v "${HAPROXY_CFG}:/usr/local/etc/haproxy/haproxy.cfg:ro" \
  haproxy:3.2 \
  haproxy -c -f /usr/local/etc/haproxy/haproxy.cfg

run_step "Ensure postgres is up" \
  compose up -d --no-deps --remove-orphans --wait-timeout "${WAIT_TIMEOUT}" --wait postgres

prepare_deploy_build_env

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
  # API must roll before dispatchers: fleet self-registration hits
  # /api/v1/internal/fleet-components on the API image.
  ROLL_SERVICES=(
    "${STORAGE_ROLL_SERVICES[@]}"
    "${APP_ROLL_SERVICES[@]}"
    "${DISPATCHER_ROLL_SERVICES[@]}"
  )
fi

for service in "${ROLL_SERVICES[@]}"; do
  run_step "Roll ${service}" roll_service "${service}"
done

run_step "Roll web replicas (web-1 and web-2 together)" roll_web_replicas

run_step "Ensure load balancer (ssh-lb) is up" \
  compose up -d --no-deps ssh-lb

run_step "Verify API health via load balancer" \
  verify_lb_api_health

run_step "Verify web UI via load balancer" \
  verify_lb_web

if production_tunnel_profile_enabled; then
  run_step "Ensure Cloudflare tunnel is up" \
    compose up -d --no-deps opengitbase_cloudflare_tunnel
fi

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

if [ "${ROLL_KAFKA}" = true ]; then
  run_step "Restart Kafka quorum (atomic, keep volumes)" \
    "${SCRIPT_DIR}/kafka-quorum-reset.sh" --restart
else
  echo "==> Skipping Kafka (pass --kafka to atomically restart the quorum)"
fi

echo ""
echo "Rolling update completed successfully."
echo "Elapsed time: $(format_elapsed "${SECONDS}")"
