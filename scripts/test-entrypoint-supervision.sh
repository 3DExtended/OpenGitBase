#!/usr/bin/env bash
#
# Regression test: the storage and dispatcher containers exit when any process their entrypoint
# supervises dies, so `restart: unless-stopped` restarts them, and `docker stop` still reaches
# every process and stops the container inside the grace period.
#
# Both entrypoints used to end in `exec /usr/sbin/sshd -D -e` (the dispatcher only with
# GIT_SSH_ENABLED=true). sshd became the container's main process and the HTTP servers its
# children, so when storage-http-server.py, nginx or the dispatcher's dotnet died, sshd kept the
# container running (storage unhealthy, the dispatcher without HTTP) and Docker never restarted it.
#
# The check runs the real docker-compose.yml services storage-1, dispatcher-1 (switched to
# GIT_SSH_ENABLED=true) and dispatcher-2 (HTTP only) in a compose project of its own, next to an
# HAProxy stub of the API that answers storage registration and heartbeats. No named volumes and
# no published ports. Processes are killed from inside the container, except the dispatcher's
# dotnet: .NET ignores a SIGABRT sent by another process, so a startup hook mounted only here makes
# it throw on a background thread.
#
# Usage:
#   scripts/test-entrypoint-supervision.sh               # build both images, then check
#   SKIP_BUILD=1 scripts/test-entrypoint-supervision.sh  # reuse the images from an earlier run
#
# Requires: docker with Compose v2.24+ (!reset / !override in the overlay), openssl, ssh-keygen

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
# shellcheck source=docker-env.sh
source "${SCRIPT_DIR}/docker-env.sh"

PROJECT="ogb-entrypoint-supervision-$$"
# Own tags, so the run never replaces the opengitbase-storage / -dispatcher images a stack uses.
STORAGE_IMAGE="opengitbase-storage:supervision-check"
DISPATCHER_IMAGE="opengitbase-dispatcher:supervision-check"
STUB_IMAGE="haproxy:3.2"
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"
TRIGGER_FILE="/tmp/ogb-supervision-abort-trigger"
ABORT_MESSAGE="ogb entrypoint supervision check: unhandled exception on a background thread"
STORAGE_ENTRYPOINT_CMDLINE="bash /usr/local/bin/entrypoint.sh"
WAIT_SECONDS="${WAIT_SECONDS:-60}"
WAIT_TIMEOUT="${WAIT_TIMEOUT:-180}"
# Compose's default stop_grace_period; neither service sets its own.
STOP_GRACE_SECONDS=10

TMP=""
STACK_STARTED=false
FAILURES=0

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

cleanup() {
  if [ "${STACK_STARTED}" = "true" ]; then
    # rm -v removes only the containers' anonymous volumes, never a named volume.
    compose rm -f -s -v >/dev/null 2>&1 || true
    compose down --remove-orphans >/dev/null 2>&1 || true
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

compose() {
  docker compose -p "${PROJECT}" \
    -f "${REPO_ROOT}/docker-compose.yml" \
    -f "${TMP}/docker-compose.supervision.yml" \
    "$@"
}

# Captured whole: `docker logs | grep -q` fails under pipefail when grep exits early.
container_logs() {
  docker logs "$1" 2>&1 || true
}

show_logs() {
  container_logs "$1" | tail -n 25 | sed 's/^/      /' >&2
}

inspect() {
  docker inspect -f "$2" "$1" 2>/dev/null || echo missing
}

health_status() {
  inspect "$1" '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}'
}

wait_healthy() {
  local container="$1" i
  for i in $(seq 1 "${WAIT_TIMEOUT}"); do
    if [ "$(health_status "${container}")" = "healthy" ]; then
      return 0
    fi
    sleep 1
  done
  return 1
}

service_container() {
  compose ps -a -q "$1"
}

# Prints the PIDs and command lines of the container's processes, for failure messages.
show_processes() {
  docker exec "$1" sh -c '
    for dir in /proc/[0-9]*; do
      read -r _ _ _ ppid _ <"${dir}/stat" 2>/dev/null || continue
      echo "${dir#/proc/} (ppid ${ppid}) $(tr "\0" " " <"${dir}/cmdline")"
    done' 2>/dev/null | sed 's/^/      /' >&2 || true
}

# Prints the PID of the container process whose command line starts with $2.
pid_by_cmdline() {
  docker exec "$1" sh -c '
    for dir in /proc/[0-9]*; do
      cmdline=$(tr "\0" " " <"${dir}/cmdline" 2>/dev/null) || continue
      case "${cmdline}" in
        "$1"*)
          echo "${dir#/proc/}"
          exit 0
          ;;
      esac
    done
    exit 1' sh "$2"
}

pid_from_file() {
  docker exec "$1" cat "$2"
}

# The storage agent's heartbeat loop is a subshell of the entrypoint, so it has the entrypoint's
# command line. It is the one whose parent is the entrypoint itself; the entrypoint's own parent
# is not an entrypoint, and short-lived subshells of the loop have the loop as their parent.
storage_agent_pid() {
  docker exec "$1" sh -c '
    pids=""
    for dir in /proc/[0-9]*; do
      cmdline=$(tr "\0" " " <"${dir}/cmdline" 2>/dev/null) || continue
      case "${cmdline}" in "$1"*) pids="${pids} ${dir#/proc/}" ;; esac
    done
    is_entrypoint() {
      for candidate in ${pids}; do
        [ "${candidate}" = "$1" ] && return 0
      done
      return 1
    }
    parent_of() {
      read -r _ _ _ ppid _ <"/proc/$1/stat" && echo "${ppid}"
    }
    for pid in ${pids}; do
      parent=$(parent_of "${pid}") || continue
      is_entrypoint "${parent}" || continue
      grandparent=$(parent_of "${parent}") || continue
      is_entrypoint "${grandparent}" && continue
      echo "${pid}"
      exit 0
    done
    exit 1' sh "${STORAGE_ENTRYPOINT_CMDLINE}"
}

kill_in_container() {
  local container="$1" signal="$2" pid="$3"
  [ -n "${pid}" ] && docker exec "${container}" kill -s "${signal}" "${pid}"
}

crash_storage_http() {
  kill_in_container "$1" ABRT "$(pid_by_cmdline "$1" "python3 /usr/local/bin/storage-http-server.py")"
}

crash_storage_nginx() {
  kill_in_container "$1" KILL "$(pid_from_file "$1" /var/run/nginx-git-http.pid)"
}

crash_sshd() {
  kill_in_container "$1" KILL "$(pid_from_file "$1" /var/run/sshd.pid)"
}

crash_storage_agent() {
  kill_in_container "$1" KILL "$(storage_agent_pid "$1")"
}

crash_dotnet() {
  docker exec "$1" touch "${TRIGGER_FILE}"
}

# The startup hook starts a thread that throws once TRIGGER_FILE exists. It deletes the file
# first: a restart keeps the container's filesystem, and the restarted process must stay up.
write_startup_hook() {
  mkdir -p "${TMP}/hook"
  cat >"${TMP}/hook/AbortStartupHook.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
EOF
  cat >"${TMP}/hook/StartupHook.cs" <<EOF
using System;
using System.IO;
using System.Threading;

internal static class StartupHook
{
    public static void Initialize()
    {
        new Thread(() =>
        {
            while (!File.Exists("${TRIGGER_FILE}"))
            {
                Thread.Sleep(200);
            }

            File.Delete("${TRIGGER_FILE}");
            throw new InvalidOperationException("${ABORT_MESSAGE}");
        })
        {
            IsBackground = true,
            Name = "entrypoint-supervision-check",
        }.Start();
    }
}
EOF
}

build_startup_hook() {
  docker run --rm --user "$(id -u):$(id -g)" \
    -e HOME=/tmp -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    -v "${TMP}/hook:/hook" -w /hook \
    "${SDK_IMAGE}" dotnet build -c Release -o /hook/out -v quiet >/dev/null
}

# Registration hands out the API token storage-http-server.py needs to start; heartbeats only
# have to succeed. Everything else (the dispatchers' fleet calls) gets a 404, which they log and
# retry.
write_api_stub() {
  mkdir -p "${TMP}/stub"
  echo '{"apiToken":"supervision-check-token","heartbeatIntervalSeconds":30}' >"${TMP}/stub/register.json"
  echo '{}' >"${TMP}/stub/heartbeat.json"
  cat >"${TMP}/stub/haproxy.cfg" <<'CFG'
global
  log stdout format raw local0

defaults
  mode http
  timeout connect 2s
  timeout client 10s
  timeout server 10s

frontend api
  bind :8080
  http-request return status 200 content-type application/json file /stub/register.json if { path /api/v1/storage-nodes/register }
  http-request return status 200 content-type application/json file /stub/heartbeat.json if { path /api/v1/storage-nodes/heartbeat }
  http-request return status 404
CFG
  chmod 755 "${TMP}/stub"
  chmod 644 "${TMP}/stub/"*
}

# Registration sends the node certificate's thumbprint, and nginx serves the mTLS port with it.
# One self-signed certificate is enough for both, so it is its own CA too.
write_node_pki() {
  mkdir -p "${TMP}/pki"
  openssl req -x509 -newkey rsa:2048 -nodes -days 2 -subj "/CN=storage-1" \
    -keyout "${TMP}/pki/node.key" -out "${TMP}/pki/node.crt" >/dev/null 2>&1
  cp "${TMP}/pki/node.crt" "${TMP}/pki/ca.crt"
  chmod 644 "${TMP}/pki/"*
  ssh-keygen -q -t ed25519 -N '' -C "supervision-check" -f "${TMP}/pki/dispatcher_ssh"
}

write_overlay() {
  local dispatcher_public_key
  dispatcher_public_key="$(cat "${TMP}/pki/dispatcher_ssh.pub")"
  cat >"${TMP}/docker-compose.supervision.yml" <<EOF
# Generated by scripts/test-entrypoint-supervision.sh for compose project ${PROJECT}.
x-dispatcher-env: &dispatcher-env
  DOTNET_STARTUP_HOOKS: /abort-hook/AbortStartupHook.dll
  DISPATCHER_API_URL: "http://api-stub:8080"
  Dispatcher__ApiUrl: "http://api-stub:8080"

services:
  api-stub:
    image: ${STUB_IMAGE}
    command: ["haproxy", "-f", "/stub/haproxy.cfg"]
    volumes:
      - ${TMP}/stub:/stub:ro
  storage-1:
    image: ${STORAGE_IMAGE}
    container_name: !reset null
    depends_on: !override
      api-stub:
        condition: service_started
    environment:
      STORAGE_API_URL: "http://api-stub:8080"
      DISPATCHER_SSH_PUBLIC_KEY: "${dispatcher_public_key}"
    # Anonymous volumes only (the image's VOLUMEs), never the stack's named ones.
    volumes: !override
      - ${TMP}/pki/node.crt:/etc/opengitbase/node.crt:ro
      - ${TMP}/pki/node.key:/etc/opengitbase/node.key:ro
      - ${TMP}/pki/ca.crt:/etc/opengitbase/ca.crt:ro
  dispatcher-1:
    image: ${DISPATCHER_IMAGE}
    container_name: !reset null
    ports: !reset []
    depends_on: !override
      api-stub:
        condition: service_started
    environment:
      <<: *dispatcher-env
      GIT_SSH_ENABLED: "true"
    # Not the stack's named key volume (opengitbase_dispatcher_ssh): cleanup removes no named volumes.
    volumes: !override
      - ${TMP}/hook/out:/abort-hook:ro
  dispatcher-2:
    image: ${DISPATCHER_IMAGE}
    container_name: !reset null
    depends_on: !override
      api-stub:
        condition: service_started
    environment:
      <<: *dispatcher-env
      GIT_SSH_ENABLED: "false"
    # Not the stack's named key volume (opengitbase_dispatcher_ssh): cleanup removes no named volumes.
    volumes: !override
      - ${TMP}/hook/out:/abort-hook:ro
EOF
}

# Crashes one supervised process (trigger is a crash_* function) and checks that the container
# died with that process's status and Docker restarted it, healthy again. evidence, when given,
# is a log line that proves the process really crashed.
check_crash_restarts() {
  local service="$1" process="$2" expected_exit="$3" trigger="$4" evidence="${5:-}"
  local container restarts_before restarts since i exit_code
  container="$(service_container "${service}")"

  if ! wait_healthy "${container}"; then
    fail "${service}: not healthy before crashing ${process}"
    show_logs "${container}"
    return
  fi
  restarts_before="$(inspect "${container}" '{{.RestartCount}}')"

  since="$(date +%s)"
  if ! "${trigger}" "${container}"; then
    fail "${service}: could not crash ${process}"
    show_processes "${container}"
    return
  fi

  restarts="${restarts_before}"
  for i in $(seq 1 "${WAIT_SECONDS}"); do
    restarts="$(inspect "${container}" '{{.RestartCount}}')"
    if [ "${restarts}" != "${restarts_before}" ]; then
      break
    fi
    sleep 1
  done

  if [ -n "${evidence}" ] && ! grep -qF "${evidence}" <<<"$(container_logs "${container}")"; then
    fail "${service}: '${evidence}' never reached the logs; ${process} did not crash"
    show_logs "${container}"
    return
  fi

  if [ "${restarts}" = "${restarts_before}" ]; then
    fail "${service}: Docker did not restart the container within ${WAIT_SECONDS}s of crashing" \
      "${process} (status $(inspect "${container}" '{{.State.Status}}')," \
      "health $(health_status "${container}"), RestartCount ${restarts})"
    show_processes "${container}"
    return
  fi
  pass "${service}: Docker restarted the container after ${process} died"

  # --until is exclusive to the nanosecond, so a die in the current second needs the next one.
  exit_code="$(docker events --since "${since}" --until "$(($(date +%s) + 1))" \
    --filter "container=${container}" --filter event=die \
    --format '{{index .Actor.Attributes "exitCode"}}' | sed -n 1p)"
  if [ "${exit_code}" = "${expected_exit}" ]; then
    pass "${service}: the container exited with ${process}'s status ${expected_exit}"
  else
    fail "${service}: expected exit code ${expected_exit} (${process}'s status), the container died with '${exit_code}'"
  fi

  if wait_healthy "${container}"; then
    pass "${service}: healthy again after the restart"
  else
    fail "${service}: not healthy within ${WAIT_TIMEOUT}s of the restart"
    show_logs "${container}"
  fi
}

# `docker stop` must reach every supervised process: each expected line (one per process that
# logs its shutdown) shows up, and the entrypoint exits 143 (it ended on SIGTERM after its
# children stopped) inside the grace period instead of being killed (137) when it runs out.
check_graceful_stop() {
  local service="$1"
  shift
  local container lines_before started elapsed exit_code stop_logs expected
  container="$(service_container "${service}")"

  if ! wait_healthy "${container}"; then
    fail "${service}: not healthy before docker stop"
    show_logs "${container}"
    return
  fi

  lines_before="$(container_logs "${container}" | wc -l)"
  started="$(date +%s)"
  compose stop -t "${STOP_GRACE_SECONDS}" "${service}" >/dev/null 2>&1
  elapsed=$(($(date +%s) - started))
  exit_code="$(inspect "${container}" '{{.State.ExitCode}}')"

  if [ "${exit_code}" = "143" ] && [ "${elapsed}" -lt "${STOP_GRACE_SECONDS}" ]; then
    pass "${service}: docker stop exited 143 after ${elapsed}s"
  else
    fail "${service}: docker stop took ${elapsed}s (grace ${STOP_GRACE_SECONDS}s), exit code ${exit_code}, want 143"
  fi

  stop_logs="$(container_logs "${container}" | tail -n +"$((lines_before + 1))")"
  for expected in "$@"; do
    if grep -qF "${expected}" <<<"${stop_logs}"; then
      pass "${service}: logged '${expected}'"
    else
      fail "${service}: no '${expected}' in the logs; SIGTERM did not reach that process"
    fi
  done
  if grep -qE '^(fail|crit): ' <<<"${stop_logs}"; then
    fail "${service}: errors were logged while it stopped"
    sed -n '1,25{s/^/      /;p;}' <<<"${stop_logs}" >&2
  fi
}

require_cmd docker
require_cmd openssl
require_cmd ssh-keygen

TMP="$(mktemp -d "${TMPDIR:-/tmp}/ogb-entrypoint-supervision.XXXXXX")"
TMP="$(cd "${TMP}" && pwd -P)"

echo "==> Build the abort startup hook (${SDK_IMAGE})"
write_startup_hook
build_startup_hook
write_api_stub
write_node_pki
write_overlay

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  echo "==> Build ${STORAGE_IMAGE} and ${DISPATCHER_IMAGE}"
  compose build storage-1 dispatcher-1
fi

echo "==> Start the API stub, storage-1, dispatcher-1 (SSH) and dispatcher-2 (project ${PROJECT})"
STACK_STARTED=true
if ! compose up -d --wait --wait-timeout "${WAIT_TIMEOUT}" api-stub storage-1 dispatcher-1 dispatcher-2; then
  echo "error: the services did not come up" >&2
  compose logs --tail 40 storage-1 dispatcher-1 dispatcher-2 >&2 || true
  exit 1
fi

SSHD_STOPPED="Received signal 15; terminating."
NGINX_STOPPED="signal 15 (SIGTERM) received"
DOTNET_STOPPED="Application is shutting down..."

echo "==> docker stop reaches every process"
check_graceful_stop storage-1 "${SSHD_STOPPED}" "${NGINX_STOPPED}"
check_graceful_stop dispatcher-1 "${DOTNET_STOPPED}" "${SSHD_STOPPED}"
check_graceful_stop dispatcher-2 "${DOTNET_STOPPED}"

echo "==> Start them again"
compose start storage-1 dispatcher-1 dispatcher-2 >/dev/null

echo "==> storage-1: a dead process restarts the container"
check_crash_restarts storage-1 storage-http-server.py 134 crash_storage_http
check_crash_restarts storage-1 nginx 137 crash_storage_nginx
check_crash_restarts storage-1 sshd 137 crash_sshd
check_crash_restarts storage-1 "the storage agent's heartbeat loop" 137 crash_storage_agent

echo "==> dispatcher-1 (GIT_SSH_ENABLED=true): a dead process restarts the container"
check_crash_restarts dispatcher-1 dotnet 134 crash_dotnet \
  "Unhandled exception. System.InvalidOperationException: ${ABORT_MESSAGE}"
check_crash_restarts dispatcher-1 sshd 137 crash_sshd

echo "==> dispatcher-2 (HTTP only): a dead dotnet restarts the container"
check_crash_restarts dispatcher-2 dotnet 134 crash_dotnet \
  "Unhandled exception. System.InvalidOperationException: ${ABORT_MESSAGE}"

echo ""
if [ "${FAILURES}" -gt 0 ]; then
  echo "${FAILURES} check(s) failed." >&2
  exit 1
fi
echo "All checks passed."
