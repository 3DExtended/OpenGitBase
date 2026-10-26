#!/usr/bin/env bash
#
# Regression test for where the Kafka KRaft data lives. docker-compose.yml and the e2e overlays
# must keep it in the kafka{N}_data named volumes so the stack starts on any Docker host
# (Docker Desktop refuses bind mounts from unshared paths such as /mnt/cache), while
# docker-compose.prod.yml bind-mounts it onto the Tower SSD cache pool (#232). Also covers the
# scripts/kafka-storage.sh helpers used by the Kafka scripts: the guard that stops a recreate from
# moving Tower's quorum back onto the array, and the wipe that empties whatever the compose files
# mount.
#
# Usage:
#   scripts/test-kafka-data-placement.sh
#
# Requires: docker with Compose v2.24+ (docker-compose.prod.yml uses !reset); the wipe check also
# needs a running Docker daemon and pulls busybox.

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
# shellcheck source=kafka-storage.sh
source "${SCRIPT_DIR}/kafka-storage.sh"

BASE="${REPO_ROOT}/docker-compose.yml"
E2E="${REPO_ROOT}/docker-compose.e2e.yml"
FULL_HA="${REPO_ROOT}/docker-compose.e2e-full-ha.yml"
PROD="${REPO_ROOT}/docker-compose.prod.yml"
TOWER_SSD_ROOT="/mnt/cache/appdata/opengitbase"
WIPE_PROJECT="ogb-kafka-wipe-test-$$"

# The compose files the helpers under test see, as -f/-p arguments.
COMPOSE_ARGS=()
TMP=""
WIPE_RAN=false
FAILURES=0

compose() {
  docker compose "${COMPOSE_ARGS[@]}" "$@"
}

cleanup() {
  if [ "${WIPE_RAN}" = "true" ]; then
    local volume
    for volume in $(docker volume ls -q --filter "label=com.docker.compose.project=${WIPE_PROJECT}" 2>/dev/null); do
      docker volume rm -f "${volume}" >/dev/null 2>&1 || true
    done
    docker network rm "${WIPE_PROJECT}_default" >/dev/null 2>&1 || true
  fi
  if [ -n "${TMP}" ] && [ -d "${TMP}" ]; then
    rm -rf "${TMP}"
  fi
}
trap cleanup EXIT

pass() {
  echo "    ok: $*"
}

fail() {
  echo "    FAIL: $*" >&2
  FAILURES=$((FAILURES + 1))
}

# Fails the case unless the resolved mounts contain the "source target" pair.
expect_mount() {
  local mounts="$1" source="$2" target="$3"
  if grep -qxF "${source} ${target}" <<<"${mounts}"; then
    pass "${source} -> ${target}"
  else
    fail "expected ${source} -> ${target}; resolved Kafka mounts were:"$'\n'"$(grep kafka <<<"${mounts}")"
  fi
}

expect_named_volumes() {
  local mounts="$1" n
  for n in 1 2 3; do
    expect_mount "${mounts}" "kafka${n}_data" /var/lib/kafka/data
    expect_mount "${mounts}" "kafka${n}_data" "/kafka${n}"
  done
}

expect_bind_root() {
  local mounts="$1" root="$2" n
  for n in 1 2 3; do
    expect_mount "${mounts}" "${root}/kafka${n}" /var/lib/kafka/data
    expect_mount "${mounts}" "${root}/kafka${n}" "/kafka${n}"
  done
}

expect_no_tower_paths() {
  local label="$1" count
  count=$(compose config | grep -c "${TOWER_SSD_ROOT}" || true)
  if [ "${count}" = "0" ]; then
    pass "${label}: no ${TOWER_SSD_ROOT} path"
  else
    fail "${label}: ${count} line(s) still reference ${TOWER_SSD_ROOT}"
  fi
}

TMP=$(mktemp -d)
SSD_ROOT="${TMP}/ssd"
mkdir -p "${SSD_ROOT}/kafka1" "${SSD_ROOT}/kafka2" "${SSD_ROOT}/kafka3"
unset OGB_KAFKA_DATA_ROOT

echo "==> Dev, CI and E2E stacks keep Kafka data in named volumes"
COMPOSE_ARGS=(-f "${BASE}")
expect_no_tower_paths "docker-compose.yml"
expect_named_volumes "$(kafka_compose_mounts)"
COMPOSE_ARGS=(-f "${BASE}" -f "${E2E}")
expect_no_tower_paths "docker-compose.yml + e2e"
expect_named_volumes "$(kafka_compose_mounts)"
COMPOSE_ARGS=(-f "${BASE}" -f "${E2E}" -f "${FULL_HA}")
expect_no_tower_paths "docker-compose.yml + e2e + e2e-full-ha"
expect_named_volumes "$(kafka_compose_mounts)"

echo "==> Production overlay bind-mounts Kafka data onto the Tower SSD cache pool"
COMPOSE_ARGS=(-f "${BASE}" -f "${PROD}")
prod_mounts=$(kafka_compose_mounts)
expect_bind_root "${prod_mounts}" "${TOWER_SSD_ROOT}"
if grep -q '^kafka[123]_data ' <<<"${prod_mounts}"; then
  fail "production config still mounts a kafka{N}_data named volume"
else
  pass "production config mounts no kafka{N}_data named volume"
fi

echo "==> OGB_KAFKA_DATA_ROOT relocates the production Kafka data"
expect_bind_root "$(OGB_KAFKA_DATA_ROOT=/srv/kafka kafka_compose_mounts)" /srv/kafka

echo "==> Tower override template, uncommented, mounts the same SSD dirs"
# Tower runs docker-compose.yml + docker-compose.override.yml, not docker-compose.prod.yml.
{
  echo "services:"
  sed -n '/^  # kafka-vol-init:/,/^  # *- .*kafka3:\/var\/lib\/kafka\/data/p' \
    "${REPO_ROOT}/docker-compose.override.example.yml" | sed 's/^  # \{0,1\}/  /'
} >"${TMP}/tower-override.yml"
COMPOSE_ARGS=(-f "${BASE}" -f "${TMP}/tower-override.yml")
expect_bind_root "$(kafka_compose_mounts)" "${TOWER_SSD_ROOT}"

echo "==> Guard refuses compose files that would move SSD-hosted Kafka data"
COMPOSE_ARGS=(-f "${BASE}")
if KAFKA_SSD_ROOT="${SSD_ROOT}" kafka_require_ssd_mounts 2>/dev/null; then
  fail "guard accepted named volumes on a host with SSD Kafka dirs"
else
  pass "guard rejects named volumes on a host with SSD Kafka dirs"
fi
cat >"${TMP}/brokers-only.yml" <<EOF
services:
  kafka-1:
    volumes:
      - ${SSD_ROOT}/kafka1:/var/lib/kafka/data
  kafka-2:
    volumes:
      - ${SSD_ROOT}/kafka2:/var/lib/kafka/data
  kafka-3:
    volumes:
      - ${SSD_ROOT}/kafka3:/var/lib/kafka/data
EOF
COMPOSE_ARGS=(-f "${BASE}" -f "${TMP}/brokers-only.yml")
if KAFKA_SSD_ROOT="${SSD_ROOT}" kafka_require_ssd_mounts 2>/dev/null; then
  fail "guard accepted kafka-vol-init on named volumes while the brokers use the SSD"
else
  pass "guard rejects kafka-vol-init on named volumes while the brokers use the SSD"
fi
COMPOSE_ARGS=(-f "${BASE}" -f "${PROD}")
if KAFKA_SSD_ROOT="${SSD_ROOT}" OGB_KAFKA_DATA_ROOT="${SSD_ROOT}" kafka_require_ssd_mounts; then
  pass "guard accepts the production overlay on an SSD host"
else
  fail "guard rejected the production overlay on an SSD host"
fi
COMPOSE_ARGS=(-f "${BASE}")
if KAFKA_SSD_ROOT="${TMP}/absent" kafka_require_ssd_mounts; then
  pass "guard accepts named volumes on a host without SSD Kafka dirs"
else
  fail "guard rejected named volumes on a host without SSD Kafka dirs"
fi

echo "==> Wipe empties the SSD Kafka dirs through kafka-vol-init"
for n in 1 2 3; do
  mkdir -p "${SSD_ROOT}/kafka${n}/__cluster_metadata-0"
  echo "node.id=${n}" >"${SSD_ROOT}/kafka${n}/meta.properties"
  echo "log" >"${SSD_ROOT}/kafka${n}/__cluster_metadata-0/00000000000000000000.log"
  echo "lock" >"${SSD_ROOT}/kafka${n}/.lock"
done
echo "not kafka" >"${SSD_ROOT}/keep.txt"
COMPOSE_ARGS=(-p "${WIPE_PROJECT}" -f "${BASE}" -f "${PROD}")
WIPE_RAN=true
if OGB_KAFKA_DATA_ROOT="${SSD_ROOT}" kafka_empty_data_dirs >/dev/null; then
  for n in 1 2 3; do
    if [ -d "${SSD_ROOT}/kafka${n}" ] && [ -z "$(ls -A "${SSD_ROOT}/kafka${n}")" ]; then
      pass "kafka${n} emptied"
    else
      fail "kafka${n} not emptied: $(ls -A "${SSD_ROOT}/kafka${n}" 2>&1 | tr '\n' ' ')"
    fi
  done
  if [ -f "${SSD_ROOT}/keep.txt" ]; then
    pass "files beside the Kafka dirs are kept"
  else
    fail "wipe deleted ${SSD_ROOT}/keep.txt beside the Kafka dirs"
  fi
else
  fail "kafka_empty_data_dirs exited non-zero"
fi

if [ "${FAILURES}" -gt 0 ]; then
  echo "==> ${FAILURES} check(s) failed" >&2
  exit 1
fi
echo "==> Kafka data placement checks passed"
