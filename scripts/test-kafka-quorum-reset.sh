#!/usr/bin/env bash
#
# Regression test for which named volumes kafka-quorum-reset.sh --wipe removes. The script has to
# take the compose project from the compose config (COMPOSE_PROJECT_NAME, a top-level name:, else
# the checkout directory), never from containers: it removes the volumes after it has removed the
# brokers, and every checkout gives them the same container_name (opengitbase_kafka_N), so a
# container lookup can return another stack's project and the wipe deletes that stack's volumes.
#
# Runs the script against stub docker and docker-compose commands that only record their calls.
# `docker-compose config` goes to the real compose, so the project is resolved the way compose
# resolves it. No Docker daemon is needed, and no container or volume is touched.
#
# Usage:
#   scripts/test-kafka-quorum-reset.sh
#
# Requires: docker-compose (Compose v2) on PATH.

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
RESET="${SCRIPT_DIR}/kafka-quorum-reset.sh"
REAL_COMPOSE=$(command -v docker-compose) || {
  echo "docker-compose not found on PATH" >&2
  exit 1
}

TMP=$(mktemp -d)
trap 'rm -rf "${TMP}"' EXIT
FAILURES=0

pass() {
  echo "    ok: $*"
}

fail() {
  echo "    FAIL: $*" >&2
  FAILURES=$((FAILURES + 1))
}

STUB_BIN="${TMP}/bin"
mkdir -p "${STUB_BIN}"

cat >"${STUB_BIN}/docker-compose" <<'EOF'
#!/usr/bin/env bash
# Records the call. `config` runs the real compose (without its top-level name: line when
# STUB_CONFIG_WITHOUT_NAME=1, as Compose v1 prints it). `ps -q` reports a container only once `up`
# ran, as compose does after kafka-quorum-reset.sh has stopped and removed the brokers.
echo "compose $*" >>"${STUB_LOG}"
args=("$@")
while [ "${1:-}" = "-f" ]; do shift 2; done
case "${1:-}" in
  config)
    if [ "${STUB_CONFIG_WITHOUT_NAME:-}" = "1" ]; then
      "${REAL_COMPOSE}" "${args[@]}" | grep -v '^name: '
      exit "${PIPESTATUS[0]}"
    fi
    exec "${REAL_COMPOSE}" "${args[@]}"
    ;;
  up) touch "${STUB_STATE}/up" ;;
  ps) [ ! -f "${STUB_STATE}/up" ] || echo stub-container ;;
esac
exit 0
EOF

cat >"${STUB_BIN}/docker" <<'EOF'
#!/usr/bin/env bash
# Records the call. Every `ps` lists exited kafka containers of another checkout's project, every
# broker is healthy, and every volume exists, so the script tries to remove each one it names.
echo "docker $*" >>"${STUB_LOG}"
case "${1:-}" in
  ps) echo opengitbase ;;
  inspect) echo healthy ;;
esac
exit 0
EOF
chmod +x "${STUB_BIN}/docker-compose" "${STUB_BIN}/docker"

export REAL_COMPOSE
export STUB_LOG="${TMP}/calls.log"
export STUB_STATE="${TMP}/state"
mkdir -p "${STUB_STATE}"
OUTPUT="${TMP}/output.log"

# Runs kafka-quorum-reset.sh in mode $1 with the stubs first on PATH and no override file; the
# remaining arguments are extra environment assignments. Returns the script's exit status.
run_reset() {
  local mode="$1"
  shift
  : >"${STUB_LOG}"
  rm -f "${STUB_STATE}/up"
  env -u COMPOSE_PROJECT_NAME PATH="${STUB_BIN}:${PATH}" OVERRIDE_FILE="${TMP}/no-override.yml" \
    WAIT_TIMEOUT=5 "$@" "${RESET}" "${mode}" --no-republish >"${OUTPUT}" 2>&1
}

removed_volumes() {
  sed -n 's/^docker volume rm -f //p' "${STUB_LOG}"
}

# What the last run did, for a failure message: its exit status ($1), the stub calls and its output.
last_run() {
  printf 'exit %s; calls:\n%s\nscript output:\n%s' "$1" "$(cat "${STUB_LOG}")" "$(cat "${OUTPUT}")"
}

expect_removed_volumes_of() {
  local label="$1" status="$2" project="$3" expected n
  expected=$(
    for n in 1 2 3; do echo "${project}_kafka${n}_data"; done
    for n in 1 2 3; do echo "${project}_opengitbase_kafka${n}_data"; done
  )
  if [ "${status}" -eq 0 ] && [ "$(removed_volumes)" = "${expected}" ]; then
    pass "${label}: removed the Kafka volumes of '${project}' only"
  else
    fail "${label}: expected exit 0 and the Kafka volumes of '${project}'; $(last_run "${status}")"
  fi
}

# The script must stop before it stops, removes or starts any container or volume.
expect_untouched() {
  local label="$1" status="$2"
  if [ "${status}" -ne 0 ] && ! grep -qE '^compose .* (stop|rm|run|up) |^docker volume rm ' "${STUB_LOG}"; then
    pass "${label}: failed before touching the stack"
  else
    fail "${label}: expected a non-zero exit with nothing stopped or removed; $(last_run "${status}")"
  fi
}

echo "==> --wipe removes the volumes of its own compose project, not of stale kafka containers"
status=0
run_reset --wipe COMPOSE_PROJECT_NAME=ogbk2e || status=$?
expect_removed_volumes_of "COMPOSE_PROJECT_NAME=ogbk2e" "${status}" ogbk2e

printf 'name: ogb-named-override\n' >"${TMP}/named-override.yml"
status=0
run_reset --wipe OVERRIDE_FILE="${TMP}/named-override.yml" || status=$?
expect_removed_volumes_of "top-level name: in the override file" "${status}" ogb-named-override

echo "==> --wipe stops before touching the stack when the project cannot be resolved"
printf 'services: [broken\n' >"${TMP}/broken-override.yml"
status=0
run_reset --wipe COMPOSE_PROJECT_NAME=ogbk2e OVERRIDE_FILE="${TMP}/broken-override.yml" || status=$?
expect_untouched "compose config fails" "${status}"

status=0
run_reset --wipe COMPOSE_PROJECT_NAME=ogbk2e STUB_CONFIG_WITHOUT_NAME=1 || status=$?
expect_untouched "compose config without a top-level name" "${status}"

echo "==> --restart does not need the project and removes no volumes"
status=0
run_reset --restart STUB_CONFIG_WITHOUT_NAME=1 || status=$?
if [ "${status}" -eq 0 ] && [ -z "$(removed_volumes)" ]; then
  pass "--restart without a resolvable project"
else
  fail "--restart: expected exit 0 and no volumes removed; $(last_run "${status}")"
fi

if [ "${FAILURES}" -ne 0 ]; then
  echo "${FAILURES} check(s) failed" >&2
  exit 1
fi
echo "==> All kafka-quorum-reset checks passed"
