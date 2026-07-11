#!/usr/bin/env bash
set -euo pipefail

ENDPOINT="${MINIO_ENDPOINT:-http://minio:9000}"
BUCKET="${LAYER_STORE_BUCKET:-opengitbase-layers}"
ACCESS_KEY="${MINIO_ROOT_USER:-opengitbase}"
SECRET_KEY="${MINIO_ROOT_PASSWORD:-opengitbase-minio-dev}"

echo "Waiting for MinIO at ${ENDPOINT}..."
for _ in $(seq 1 60); do
  if mc alias set local "${ENDPOINT}" "${ACCESS_KEY}" "${SECRET_KEY}" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if ! mc ls "local/${BUCKET}" >/dev/null 2>&1; then
  echo "Creating bucket ${BUCKET}..."
  mc mb "local/${BUCKET}"
else
  echo "Bucket ${BUCKET} already exists."
fi

echo "Layer Store bucket ready."
