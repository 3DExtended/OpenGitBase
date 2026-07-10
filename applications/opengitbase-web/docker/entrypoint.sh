#!/usr/bin/env sh
set -eu

API_URL="${FLEET_API_URL:-http://api-lb:8080}"
INSTANCE_ID="${FleetComponent__InstanceId:-${HOSTNAME:-web}}"
PROBE_URL="${FleetComponent__ProbeUrl:-http://${INSTANCE_ID}:8080/health}"
HEARTBEAT_INTERVAL="${FleetComponent__HeartbeatIntervalSeconds:-30}"

register_component() {
  curl -fsS -X POST "${API_URL}/api/v1/internal/fleet-components/register" \
    -H 'Content-Type: application/json' \
    -d "{\"componentType\":\"Website\",\"instanceId\":\"${INSTANCE_ID}\",\"probeUrl\":\"${PROBE_URL}\"}" \
    >/dev/null
}

heartbeat_component() {
  curl -fsS -X POST "${API_URL}/api/v1/internal/fleet-components/heartbeat" \
    -H 'Content-Type: application/json' \
    -d "{\"componentType\":\"Website\",\"instanceId\":\"${INSTANCE_ID}\"}" \
    >/dev/null
}

heartbeat_loop() {
  while true; do
    sleep "${HEARTBEAT_INTERVAL}"
    if ! heartbeat_component; then
      register_component || true
    fi
  done
}

if [ "${FleetComponent__SelfRegistrationEnabled:-true}" != "false" ]; then
  register_component || true
  heartbeat_loop &
fi

exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile
