#!/usr/bin/env bash
set -euo pipefail

BOOTSTRAP="${KAFKA_BOOTSTRAP_SERVERS:-kafka-1:29092,kafka-2:29092,kafka-3:29092}"
TOPICS=(git.push.received ci.job.available ci.job.cancelled)

echo "Waiting for Kafka cluster at ${BOOTSTRAP}..."
for _ in $(seq 1 60); do
  if /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server "${BOOTSTRAP}" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

for topic in "${TOPICS[@]}"; do
  echo "Ensuring topic ${topic} exists (RF=3)..."
  /opt/kafka/bin/kafka-topics.sh \
    --bootstrap-server "${BOOTSTRAP}" \
    --create \
    --if-not-exists \
    --topic "${topic}" \
    --partitions 3 \
    --replication-factor 3
done

echo "Kafka topics ready."
