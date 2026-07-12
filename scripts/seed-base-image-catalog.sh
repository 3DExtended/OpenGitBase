#!/usr/bin/env bash
# Back-compat wrapper — builds a real rootfs via build-base-image-rootfs.sh when Docker is available.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if command -v docker >/dev/null 2>&1; then
  exec "${SCRIPT_DIR}/build-base-image-rootfs.sh" "$@"
fi

echo "Docker not available; falling back to minimal alpine-seed tarball." >&2
API_URL="${API_URL:-http://localhost:8089}"
MINIO_ENDPOINT="${MINIO_ENDPOINT:-http://localhost:9000}"
BUCKET="${LAYER_STORE_BUCKET:-opengitbase-layers}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-change-me-admin}"
SLUG="${BASE_IMAGE_SLUG:-alpine}"
OCI="${BASE_IMAGE_OCI:-docker.io/library/alpine:3.20}"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT
ROOTFS="${WORK_DIR}/rootfs.tar.gz"
mkdir -p "${WORK_DIR}/root/usr/local/bin"
cp "${SCRIPT_DIR}/../applications/OpenGitBase.ComputeGuestAgent/ogb-guest-agent.sh" "${WORK_DIR}/root/usr/local/bin/ogb-guest-agent"
chmod +x "${WORK_DIR}/root/usr/local/bin/ogb-guest-agent"
echo "alpine-seed" > "${WORK_DIR}/root/.ogb-base-image"
tar -C "${WORK_DIR}/root" -czf "${ROOTFS}" .
CONTENT_HASH="$(shasum -a 256 "${ROOTFS}" | awk '{print $1}')"
curl -fsS -X PUT "${MINIO_ENDPOINT}/${BUCKET}/${CONTENT_HASH}" --data-binary @"${ROOTFS}"
TOKEN="$(curl -fsS -X POST "${API_URL}/api/signin/login" -H "Content-Type: application/json" -d "{\"username\":\"${ADMIN_USER}\",\"password\":\"${ADMIN_PASS}\"}" | tr -d '\n\r"')"
curl -fsS -X POST "${API_URL}/api/admin/pipeline/base-images" -H "Authorization: Bearer ${TOKEN}" -H "Content-Type: application/json" -d "{\"slug\":\"${SLUG}\",\"versionLabel\":\"3.20\",\"artifactUri\":\"${CONTENT_HASH}\",\"contentHash\":\"${CONTENT_HASH}\",\"ociProvenance\":\"${OCI}\"}" >/dev/null
echo "Base image catalog seeded (fallback tarball)."
