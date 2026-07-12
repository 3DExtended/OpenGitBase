#!/usr/bin/env bash
# Bootstrap an organization-contributed storage node on a generic Linux host.
set -euo pipefail

OPENGITBASE_REPO_URL="${OPENGITBASE_REPO_URL:-https://github.com/3DExtended/OpenGitBase.git}"
OPENGITBASE_REPO_REF="${OPENGITBASE_REPO_REF:-main}"
WORK_DIR="${WORK_DIR:-${HOME}/.opengitbase/org-storage-node}"
IMAGE_NAME="${IMAGE_NAME:-opengitbase-org-storage}"
CONTAINER_NAME=""

ENROLLMENT_TOKEN=""
NODE_ID=""
API_URL=""
INTERNAL_HOST=""
SSH_PORT=22
HTTP_PORT=8081
GIT_HTTP_PORT=8082
MTLS_PORT=8443
DRY_RUN=0

usage() {
  cat <<'EOF'
Usage: bootstrap-org-storage-node.sh [options]

Required:
  --token TOKEN           Storage enrollment token from org settings UI
  --node-id ID            Node ID (must match enrollment)
  --api-url URL           OpenGitBase API base URL (e.g. https://api.example.com)
  --internal-host HOST    Hostname/IP dispatchers use to reach this node

Optional:
  --ssh-port PORT         Published SSH port (default: 22)
  --http-port PORT        Published internal HTTP port (default: 8081)
  --git-http-port PORT    Published git HTTP port (default: 8082)
  --mtls-port PORT        Published peer mTLS port (default: 8443)
  --work-dir PATH         Install directory (default: ~/.opengitbase/org-storage-node)
  --repo-url URL          Git clone URL (default: OpenGitBase GitHub)
  --repo-ref REF          Git ref to clone (default: main)
  --dry-run               Print actions without executing build/run
  -h, --help              Show this help

Environment:
  OPENGITBASE_REPO_URL    Override default clone URL
  OPENGITBASE_REPO_REF    Override default git ref

Requires: docker, git, openssl, curl, python3
EOF
}

log() {
  printf '==> %s\n' "$*"
}

die() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "missing required command: $1"
}

parse_args() {
  while [ $# -gt 0 ]; do
    case "$1" in
      --token)
        ENROLLMENT_TOKEN="${2:-}"
        shift 2
        ;;
      --node-id)
        NODE_ID="${2:-}"
        shift 2
        ;;
      --api-url)
        API_URL="${2:-}"
        shift 2
        ;;
      --internal-host)
        INTERNAL_HOST="${2:-}"
        shift 2
        ;;
      --ssh-port)
        SSH_PORT="${2:-}"
        shift 2
        ;;
      --http-port)
        HTTP_PORT="${2:-}"
        shift 2
        ;;
      --git-http-port)
        GIT_HTTP_PORT="${2:-}"
        shift 2
        ;;
      --mtls-port)
        MTLS_PORT="${2:-}"
        shift 2
        ;;
      --work-dir)
        WORK_DIR="${2:-}"
        shift 2
        ;;
      --repo-url)
        OPENGITBASE_REPO_URL="${2:-}"
        shift 2
        ;;
      --repo-ref)
        OPENGITBASE_REPO_REF="${2:-}"
        shift 2
        ;;
      --dry-run)
        DRY_RUN=1
        shift
        ;;
      -h | --help)
        usage
        exit 0
        ;;
      *)
        die "unknown argument: $1 (try --help)"
        ;;
    esac
  done

  [ -n "${ENROLLMENT_TOKEN}" ] || die "--token is required"
  [ -n "${NODE_ID}" ] || die "--node-id is required"
  [ -n "${API_URL}" ] || die "--api-url is required"
  [ -n "${INTERNAL_HOST}" ] || die "--internal-host is required"

  CONTAINER_NAME="opengitbase_org_storage_${NODE_ID//[^a-zA-Z0-9_.-]/_}"
}

normalize_api_url() {
  API_URL="${API_URL%/}"
  case "${API_URL}" in
    */api) ;;
    */api/v1) API_URL="${API_URL%/v1}" ;;
    *) API_URL="${API_URL}/api" ;;
  esac
}

issue_node_pki() {
  local pki_dir="${WORK_DIR}/pki"
  mkdir -p "${pki_dir}"
  local ca_key="${pki_dir}/ca.key"
  local ca_cert="${pki_dir}/ca.crt"
  local node_key="${pki_dir}/${NODE_ID}.key"
  local node_cert="${pki_dir}/${NODE_ID}.crt"

  if [ ! -f "${ca_key}" ]; then
    log "Generating local CA"
    openssl genrsa -out "${ca_key}" 4096
    openssl req -x509 -new -nodes -key "${ca_key}" -sha256 -days 3650 \
      -subj "/CN=OpenGitBase Org Storage CA" \
      -out "${ca_cert}"
  fi

  if [ ! -f "${node_cert}" ]; then
    log "Generating node certificate for ${NODE_ID}"
    local csr="${pki_dir}/${NODE_ID}.csr"
    openssl genrsa -out "${node_key}" 2048
    openssl req -new -key "${node_key}" -out "${csr}" -subj "/CN=${NODE_ID}"
    openssl x509 -req -in "${csr}" -CA "${ca_cert}" -CAkey "${ca_key}" -CAcreateserial \
      -out "${node_cert}" -days 825 -sha256
    rm -f "${csr}"
  fi
}

fetch_dispatcher_public_key() {
  log "Fetching dispatcher SSH public key"
  curl -fsS "${API_URL}/v1/storage-nodes/bootstrap/dispatcher-ssh-public-key" \
    -H "X-Storage-Enrollment-Token: ${ENROLLMENT_TOKEN}" \
    -H "X-Storage-Node-Id: ${NODE_ID}" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin).get("publicKey",""))'
}

clone_or_update_repo() {
  local repo_dir="${WORK_DIR}/source"
  if [ -d "${repo_dir}/.git" ]; then
    log "Updating existing clone"
    git -C "${repo_dir}" fetch --depth 1 origin "${OPENGITBASE_REPO_REF}"
    git -C "${repo_dir}" checkout "${OPENGITBASE_REPO_REF}"
    git -C "${repo_dir}" pull --ff-only origin "${OPENGITBASE_REPO_REF}" || true
  else
    log "Cloning OpenGitBase (${OPENGITBASE_REPO_REF})"
    git clone --depth 1 --branch "${OPENGITBASE_REPO_REF}" "${OPENGITBASE_REPO_URL}" "${repo_dir}"
  fi
  printf '%s' "${repo_dir}"
}

run_container() {
  local repo_dir="$1"
  local dispatcher_key="$2"
  local pki_dir="${WORK_DIR}/pki"
  local data_dir="${WORK_DIR}/data/${NODE_ID}"

  mkdir -p "${data_dir}"

  log "Building storage agent image"
  docker build -t "${IMAGE_NAME}" "${repo_dir}/applications/repo-storage-layer"

  if docker ps -a --format '{{.Names}}' | grep -qx "${CONTAINER_NAME}"; then
    log "Removing existing container ${CONTAINER_NAME}"
    docker rm -f "${CONTAINER_NAME}" >/dev/null
  fi

  log "Starting storage agent container ${CONTAINER_NAME}"
  docker run -d \
    --name "${CONTAINER_NAME}" \
    --restart unless-stopped \
    -p "${SSH_PORT}:22" \
    -p "${HTTP_PORT}:8081" \
    -p "${GIT_HTTP_PORT}:8082" \
    -p "${MTLS_PORT}:8443" \
    -v "${data_dir}:/srv/git" \
    -v "${pki_dir}/${NODE_ID}.crt:/etc/opengitbase/node.crt:ro" \
    -v "${pki_dir}/${NODE_ID}.key:/etc/opengitbase/node.key:ro" \
    -v "${pki_dir}/ca.crt:/etc/opengitbase/ca.crt:ro" \
    -e "STORAGE_API_URL=${API_URL}" \
    -e "STORAGE_NODE_ID=${NODE_ID}" \
    -e "STORAGE_INTERNAL_HOST=${INTERNAL_HOST}" \
    -e "STORAGE_INTERNAL_HTTP_PORT=${HTTP_PORT}" \
    -e "STORAGE_INTERNAL_GIT_HTTP_PORT=${GIT_HTTP_PORT}" \
    -e "STORAGE_GIT_HTTP_PORT=${GIT_HTTP_PORT}" \
    -e "STORAGE_ENROLLMENT_TOKEN=${ENROLLMENT_TOKEN}" \
    -e "DISPATCHER_SSH_PUBLIC_KEY=${dispatcher_key}" \
    "${IMAGE_NAME}" >/dev/null

  log "Container started. Tail logs with: docker logs -f ${CONTAINER_NAME}"
  log "Ensure firewall allows inbound TCP ${SSH_PORT}, ${HTTP_PORT}, ${GIT_HTTP_PORT}, ${MTLS_PORT} to this host."
}

main() {
  parse_args "$@"
  require_cmd docker
  require_cmd git
  require_cmd openssl
  require_cmd curl
  require_cmd python3

  normalize_api_url
  mkdir -p "${WORK_DIR}"

  issue_node_pki

  if [ "${DRY_RUN}" -eq 1 ]; then
    log "Dry run OK — prerequisites met, PKI ready under ${WORK_DIR}/pki"
    exit 0
  fi

  local dispatcher_key
  dispatcher_key="$(fetch_dispatcher_public_key)"
  [ -n "${dispatcher_key}" ] || die "failed to fetch dispatcher SSH public key"

  local repo_dir
  repo_dir="$(clone_or_update_repo)"
  run_container "${repo_dir}" "${dispatcher_key}"
}

main "$@"
