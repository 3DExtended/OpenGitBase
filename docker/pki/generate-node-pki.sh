#!/usr/bin/env bash
# Generate a local CA and per-storage-node certificates for docker compose.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CA_KEY="${DIR}/ca.key"
CA_CERT="${DIR}/ca.crt"

mkdir -p "${DIR}"

if [ ! -f "${CA_KEY}" ]; then
  openssl genrsa -out "${CA_KEY}" 4096
  openssl req -x509 -new -nodes -key "${CA_KEY}" -sha256 -days 3650 \
    -subj "/CN=OpenGitBase Storage CA" \
    -out "${CA_CERT}"
fi

issue_node_cert() {
  local node_id="$1"
  local key="${DIR}/${node_id}.key"
  local csr="${DIR}/${node_id}.csr"
  local cert="${DIR}/${node_id}.crt"

  if [ -f "${cert}" ]; then
    return
  fi

  openssl genrsa -out "${key}" 2048
  openssl req -new -key "${key}" -out "${csr}" -subj "/CN=${node_id}"
  openssl x509 -req -in "${csr}" -CA "${CA_CERT}" -CAkey "${CA_KEY}" -CAcreateserial \
    -out "${cert}" -days 825 -sha256
  rm -f "${csr}"
}

issue_node_cert storage-1
issue_node_cert storage-2

echo "Generated node certificates in ${DIR}"
