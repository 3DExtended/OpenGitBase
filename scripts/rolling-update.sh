#!/usr/bin/env bash
# Zero-downtime rolling update for the local Docker Compose stack.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"
HAPROXY_CFG="${REPO_ROOT}/docker/haproxy/haproxy.cfg"

API_URL="${API_URL:-http://localhost:8089}"
WEB_URL="${WEB_URL:-http://localhost:3000}"

SKIP_TUNNEL_CHECK=false

usage() {
  cat <<EOF
Usage: $(basename "$0") [--skip-tunnel-check]

Rebuild images and roll services one at a time for zero-downtime updates.
Compose: docker-compose.yml plus docker-compose.override.yml when present.

Options:
  --skip-tunnel-check   Skip Cloudflare tunnel API check when tunnel is running
  -h, --help            Show this help
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --skip-tunnel-check)
      SKIP_TUNNEL_CHECK=true
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
  docker compose "${COMPOSE_ARGS[@]}" "$@"
}

ROLL_SERVICES=(
  storage-1
  storage-2
  dispatcher-1
  dispatcher-2
  api-2
  api-1
  web-2
  web-1
)

BUILD_SERVICES=(
  storage-1
  storage-2
  dispatcher-1
  dispatcher-2
  api-1
  api-2
  web-1
  web-2
)

FAILED_STEP=""

recovery_guidance() {
  cat >&2 <<'EOF'

Recovery (do not run 'docker compose down' — healthy containers should still be serving traffic):
  1. Read logs for the failing service:
       docker compose logs --tail=100 <service>
  2. Fix the underlying issue (build error, migration, health check, config).
  3. Re-run this script from the repo root:
       ./scripts/rolling-update.sh
  For a full wipe and re-seed (destructive), use only when intentional:
       docker compose down -v && docker compose up -d --build
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

echo "==> Zero-downtime rolling Docker Compose update"
echo "    Repo: ${REPO_ROOT}"

run_step "Validate HAProxy configuration" \
  docker run --rm \
  -v "${HAPROXY_CFG}:/usr/local/etc/haproxy/haproxy.cfg:ro" \
  haproxy:3.2 \
  haproxy -c -f /usr/local/etc/haproxy/haproxy.cfg

run_step "Ensure postgres is up" \
  compose up -d --wait postgres

run_step "Build service images" \
  compose build "${BUILD_SERVICES[@]}"

run_step "Run database migrations (api-migrate)" \
  compose --profile tools run --rm --no-deps api-migrate

for service in "${ROLL_SERVICES[@]}"; do
  run_step "Roll ${service}" \
    compose up -d --build --no-deps --wait "${service}"
done

run_step "Verify API health via load balancer (${API_URL}/health)" \
  curl -fsS "${API_URL}/health" >/dev/null

run_step "Verify web UI via load balancer (${WEB_URL}/)" \
  curl -fsS "${WEB_URL}/" >/dev/null

if [ "${SKIP_TUNNEL_CHECK}" = false ] && container_running opengitbase_cloudflare_tunnel; then
  run_step "Verify API via HAProxy internal path (tunnel stack)" \
    docker exec opengitbase_ssh_lb wget -qO- http://127.0.0.1:8080/health >/dev/null
elif [ "${SKIP_TUNNEL_CHECK}" = true ]; then
  echo "==> Skipping Cloudflare tunnel API check (--skip-tunnel-check)"
else
  echo "==> Cloudflare tunnel not running; skipping tunnel API check"
fi

echo ""
echo "Rolling update completed successfully."
