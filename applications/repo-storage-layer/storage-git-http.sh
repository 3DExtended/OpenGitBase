#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0

set -euo pipefail

GIT_HTTP_PORT="${STORAGE_GIT_HTTP_PORT:-8082}"
NGINX_CONF="/etc/opengitbase/nginx-git-http.conf"
FCGI_DISPATCHER_SOCKET="/var/run/fcgiwrap-dispatcher.socket"
FCGI_MTLS_SOCKET="/var/run/fcgiwrap-mtls.socket"
# Separate pools so post-receive on :8082 cannot block peer replication fetches on :8443.
FCGI_DISPATCHER_WORKERS="${STORAGE_FCGI_DISPATCHER_WORKERS:-2}"
FCGI_MTLS_WORKERS="${STORAGE_FCGI_MTLS_WORKERS:-2}"

if [ ! -x /usr/lib/git-core/git-http-backend ]; then
  echo "storage-git-http: git-http-backend not found" >&2
  exit 1
fi

mkdir -p /var/run
rm -f "${FCGI_DISPATCHER_SOCKET}" "${FCGI_MTLS_SOCKET}"

spawn-fcgi -F "${FCGI_DISPATCHER_WORKERS}" -s "${FCGI_DISPATCHER_SOCKET}" -M 0666 -u git -g git -- /usr/sbin/fcgiwrap
spawn-fcgi -F "${FCGI_MTLS_WORKERS}" -s "${FCGI_MTLS_SOCKET}" -M 0666 -u git -g git -- /usr/sbin/fcgiwrap

# nginx-git-http.conf hardcodes 8082; regenerate listen when port differs.
if [ "${GIT_HTTP_PORT}" != "8082" ]; then
  sed "s/listen 8082;/listen ${GIT_HTTP_PORT};/" "${NGINX_CONF}" > /tmp/nginx-git-http.conf
  NGINX_CONF="/tmp/nginx-git-http.conf"
fi

echo "storage-git-http: listening on 0.0.0.0:${GIT_HTTP_PORT}"
exec nginx -g 'daemon off;' -c "${NGINX_CONF}"
