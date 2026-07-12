#!/usr/bin/env bash
# Build a curated Base Image rootfs from a pinned OCI source, upload to Layer Store,
# and create or update a Base Image Catalog entry.
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
MINIO_ENDPOINT="${MINIO_ENDPOINT:-http://localhost:9000}"
BUCKET="${LAYER_STORE_BUCKET:-opengitbase-layers}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-change-me-admin}"
SLUG="${BASE_IMAGE_SLUG:-alpine}"
OCI="${BASE_IMAGE_OCI:-docker.io/library/alpine:3.20}"
VERSION_LABEL="${BASE_IMAGE_VERSION:-3.20}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

echo "==> Building rootfs image from ${OCI}"
docker build \
  --build-arg "BASE_IMAGE=${OCI}" \
  -f "${REPO_ROOT}/docker/base-image/Dockerfile" \
  -t "opengitbase/base-image:${SLUG}" \
  "${REPO_ROOT}"

CONTAINER_ID="$(docker create "opengitbase/base-image:${SLUG}")"
trap 'docker rm -f "${CONTAINER_ID}" >/dev/null 2>&1 || true; rm -rf "${WORK_DIR}"' EXIT
ROOTFS="${WORK_DIR}/rootfs.tar.gz"
docker export "${CONTAINER_ID}" | gzip -1 > "${ROOTFS}"
docker rm -f "${CONTAINER_ID}" >/dev/null

echo "==> Verifying rootfs markers"
tar -tzf "${ROOTFS}" | grep -q 'usr/local/bin/ogb-guest-agent'
tar -xOzf "${ROOTFS}" etc/passwd | grep -q '^ogb:'

CONTENT_HASH="$(shasum -a 256 "${ROOTFS}" | awk '{print $1}')"
echo "==> Content hash: ${CONTENT_HASH}"

echo "==> Uploading to Layer Store (${MINIO_ENDPOINT}/${BUCKET}/${CONTENT_HASH})"
curl -fsS -X PUT "${MINIO_ENDPOINT}/${BUCKET}/${CONTENT_HASH}" \
  --data-binary @"${ROOTFS}"

TOKEN="$(curl -fsS -X POST "${API_URL}/api/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${ADMIN_USER}\",\"password\":\"${ADMIN_PASS}\"}" \
  | tr -d '\n\r"')"

echo "==> Creating catalog entry for slug ${SLUG}"
curl -fsS -X POST "${API_URL}/api/admin/pipeline/base-images" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"slug\":\"${SLUG}\",\"versionLabel\":\"${VERSION_LABEL}\",\"artifactUri\":\"${CONTENT_HASH}\",\"contentHash\":\"${CONTENT_HASH}\",\"ociProvenance\":\"${OCI}\"}" \
  >/dev/null

echo "==> Verifying resolve endpoint"
curl -fsS "${API_URL}/api/pipeline/base-images/resolve?slug=${SLUG}" | python3 -m json.tool
echo "Base image rootfs built and catalog updated."
