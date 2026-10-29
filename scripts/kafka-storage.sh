#!/usr/bin/env bash
# Kafka data placement helpers, sourced by the Kafka scripts. Callers define compose().
#
# docker-compose.yml keeps each broker's KRaft log in a kafka{N}_data named volume so the stack
# starts on any Docker host. The Unraid production host (Tower) bind-mounts those dirs onto the
# SSD cache pool instead, via docker-compose.prod.yml or its docker-compose.override.yml: on the
# array-backed docker.img the metadata log's fsyncs blow the 2s controller.quorum.request.timeout.ms
# and the quorum churns elections forever (#232).

# Where Tower keeps the Kafka data. A host that has ${KAFKA_SSD_ROOT}/kafka1 is treated as Tower.
KAFKA_SSD_ROOT="${KAFKA_SSD_ROOT:-/mnt/cache/appdata/opengitbase}"

# Prints one "source target" line per mount in the resolved compose config.
kafka_compose_mounts() {
  compose config | awk '
    /^ *source: / { source = $2; next }
    /^ *target: / { if (source != "") print source, $2; source = "" }
  '
}

# Fails when this host has the SSD Kafka dirs but the compose files in use would mount something
# else for a broker or for kafka-vol-init, so a recreate or a wipe cannot silently move Tower's
# quorum back onto the array (or empty the wrong dirs).
kafka_require_ssd_mounts() {
  [ -d "${KAFKA_SSD_ROOT}/kafka1" ] || return 0

  local mounts n missing=()
  mounts="$(kafka_compose_mounts)" || return 1
  for n in 1 2 3; do
    grep -qxF "${KAFKA_SSD_ROOT}/kafka${n} /var/lib/kafka/data" <<<"${mounts}" \
      || missing+=("kafka-${n}: ${KAFKA_SSD_ROOT}/kafka${n} -> /var/lib/kafka/data")
    grep -qxF "${KAFKA_SSD_ROOT}/kafka${n} /kafka${n}" <<<"${mounts}" \
      || missing+=("kafka-vol-init: ${KAFKA_SSD_ROOT}/kafka${n} -> /kafka${n}")
  done
  [ "${#missing[@]}" -eq 0 ] && return 0

  {
    echo "error: this host keeps Kafka data under ${KAFKA_SSD_ROOT}, but the compose files in use do not mount:"
    printf '    %s\n' "${missing[@]}"
    echo "Recreating the brokers now would move the KRaft quorum onto the array and bring back the"
    echo "election churn from #232. Copy the kafka-vol-init and kafka-1/2/3 volumes from"
    echo "docker-compose.prod.yml into docker-compose.override.yml (docs/deployment/kafka-operations.md)."
  } >&2
  return 1
}

# Empties each broker's data dir through kafka-vol-init, whose mounts follow the compose files in
# use: the SSD bind dirs on Tower, the kafka{N}_data named volumes elsewhere.
kafka_empty_data_dirs() {
  compose run --rm --no-deps kafka-vol-init sh -c 'find /kafka1 /kafka2 /kafka3 -mindepth 1 -delete'
}
