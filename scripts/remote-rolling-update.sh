#!/usr/bin/env bash
# Run rolling-update.sh on a production host without tying it to a long-lived SSH session.
# Builds can take hours; router/NAT or sshd may drop interactive sessions mid-deploy.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOST="${OPENGITBASE_DEPLOY_HOST:-root@tower.local}"
REPO_PATH="${OPENGITBASE_DEPLOY_REPO:-/mnt/user/projects/openGitBase}"
SSH_OPTS=(-o ServerAliveInterval=30 -o ServerAliveCountMax=240 -o TCPKeepAlive=yes)

GIT_PULL=true
ROLL_ARGS=()

usage() {
  cat <<EOF
Usage: $(basename "$0") [--full] [--skip-tunnel-check] [--prune-cache] [--no-pull]

Starts scripts/rolling-update.sh on ${HOST} under nohup and streams the log until it
finishes. Safe for multi-hour full fleet deploys over SSH.

Environment:
  OPENGITBASE_DEPLOY_HOST   SSH target (default: root@tower.local)
  OPENGITBASE_DEPLOY_REPO   Repo path on host (default: /mnt/user/projects/openGitBase)
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --full | --skip-tunnel-check | --prune-cache)
      ROLL_ARGS+=("$1")
      shift
      ;;
    --no-pull)
      GIT_PULL=false
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

roll_args_shell() {
  if [ "${#ROLL_ARGS[@]}" -eq 0 ]; then
    printf ''
    return
  fi
  printf '%q ' "${ROLL_ARGS[@]}"
}

REMOTE_LOG="/tmp/opengitbase-rolling-update.log"
REMOTE_PID="/tmp/opengitbase-rolling-update.pid"
REMOTE_EXIT="/tmp/opengitbase-rolling-update.exit"

echo "==> Starting detached rolling update on ${HOST}"
echo "    Repo: ${REPO_PATH}"
if [ "${#ROLL_ARGS[@]}" -gt 0 ]; then
  echo "    Args: ${ROLL_ARGS[*]}"
fi

ssh "${SSH_OPTS[@]}" "${HOST}" bash -s -- "${REPO_PATH}" "${REMOTE_LOG}" "${REMOTE_PID}" "${REMOTE_EXIT}" "${GIT_PULL}" "$(roll_args_shell)" <<'REMOTE'
set -euo pipefail

REPO_PATH="$1"
REMOTE_LOG="$2"
REMOTE_PID="$3"
REMOTE_EXIT="$4"
GIT_PULL="$5"
ROLL_ARGS="$6"

if [ -f "${REMOTE_PID}" ]; then
  existing_pid="$(cat "${REMOTE_PID}")"
  if kill -0 "${existing_pid}" 2>/dev/null; then
    echo "Another rolling update is already running (pid ${existing_pid})." >&2
    echo "Tail ${REMOTE_LOG} or stop pid ${existing_pid} before retrying." >&2
    exit 1
  fi
fi

cd "${REPO_PATH}"

if [ "${GIT_PULL}" = true ]; then
  echo "==> git pull --ff-only"
  git pull --ff-only
fi

# shellcheck source=docker-env.production.sh
source scripts/docker-env.production.sh

rm -f "${REMOTE_EXIT}"
nohup bash -c "
  set -euo pipefail
  cd \"${REPO_PATH}\"
  source scripts/docker-env.production.sh
  ./scripts/rolling-update.sh ${ROLL_ARGS}
  code=\$?
  echo \"\${code}\" > \"${REMOTE_EXIT}\"
  exit \"\${code}\"
" >"${REMOTE_LOG}" 2>&1 &

deploy_pid=$!
echo "${deploy_pid}" >"${REMOTE_PID}"
echo "LOG=${REMOTE_LOG}"
echo "PID=${deploy_pid}"
REMOTE

echo "==> Streaming ${REMOTE_LOG} (Ctrl+C detaches; deploy continues on host)"
offset=0
while true; do
  if ! ssh "${SSH_OPTS[@]}" "${HOST}" "kill -0 \"\$(cat '${REMOTE_PID}')\" 2>/dev/null"; then
    break
  fi

  chunk="$(ssh "${SSH_OPTS[@]}" "${HOST}" "tail -c +$((offset + 1)) '${REMOTE_LOG}' 2>/dev/null || true")"
  if [ -n "${chunk}" ]; then
    printf '%s' "${chunk}"
    offset=$((offset + ${#chunk}))
  fi
  sleep 5
done

ssh "${SSH_OPTS[@]}" "${HOST}" "tail -c +$((offset + 1)) '${REMOTE_LOG}' 2>/dev/null || true"

exit_code="$(ssh "${SSH_OPTS[@]}" "${HOST}" "cat '${REMOTE_EXIT}' 2>/dev/null || echo 1")"
ssh "${SSH_OPTS[@]}" "${HOST}" "rm -f '${REMOTE_PID}'" || true

if [ "${exit_code}" = "0" ]; then
  echo ""
  echo "Remote rolling update completed successfully."
  exit 0
fi

echo ""
echo "Remote rolling update failed (exit ${exit_code}). See ${REMOTE_LOG} on ${HOST}." >&2
exit "${exit_code}"
