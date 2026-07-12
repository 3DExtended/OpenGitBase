#!/usr/bin/env bash
# Seed alpine base image catalog entry and upload rootfs artifact to Layer Store (MinIO).
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
MINIO_ENDPOINT="${MINIO_ENDPOINT:-http://localhost:9000}"
BUCKET="${LAYER_STORE_BUCKET:-opengitbase-layers}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-change-me-admin}"
SLUG="${BASE_IMAGE_SLUG:-alpine}"
OCI="${BASE_IMAGE_OCI:-docker.io/library/alpine:3.20}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

ROOTFS="${WORK_DIR}/rootfs.tar.gz"
echo "==> Building minimal rootfs tarball"
mkdir -p "${WORK_DIR}/root"
echo "alpine-seed" > "${WORK_DIR}/root/.ogb-base-image"
tar -C "${WORK_DIR}/root" -czf "${ROOTFS}" .

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
  -d "{\"slug\":\"${SLUG}\",\"versionLabel\":\"3.20\",\"artifactUri\":\"${CONTENT_HASH}\",\"contentHash\":\"${CONTENT_HASH}\",\"ociProvenance\":\"${OCI}\"}" \
  >/dev/null

echo "==> Verifying resolve endpoint"
curl -fsS "${API_URL}/api/pipeline/base-images/resolve?slug=${SLUG}" | python3 -m json.tool
echo "Base image catalog seeded."
