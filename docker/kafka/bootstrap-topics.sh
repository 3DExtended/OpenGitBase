#!/usr/bin/env bash
set -euo pipefail

BOOTSTRAP="${KAFKA_BOOTSTRAP_SERVERS:-kafka-1:29092,kafka-2:29092,kafka-3:29092}"
TOPICS=(git.push.received ci.job.available ci.job.cancelled)
MAX_TOPIC_ATTEMPTS="${MAX_TOPIC_ATTEMPTS:-10}"
RETRY_DELAY_SECONDS="${RETRY_DELAY_SECONDS:-3}"

echo "Waiting for Kafka cluster at ${BOOTSTRAP}..."
for _ in $(seq 1 60); do
  if /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server "${BOOTSTRAP}" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

# A broker that answers api-versions can still reject --create with a Timeout/Disconnect
# error while the cluster is mid-quorum-formation. This container has restart:"no" and
# nothing else retries topic creation, so a single failed attempt used to leave the
# cluster permanently missing topics until an operator noticed and manually re-ran it.
create_topic_with_retry() {
  local topic="$1"
  local attempt=1
  while [ "${attempt}" -le "${MAX_TOPIC_ATTEMPTS}" ]; do
    if /opt/kafka/bin/kafka-topics.sh \
      --bootstrap-server "${BOOTSTRAP}" \
      --create \
      --if-not-exists \
      --topic "${topic}" \
      --partitions 3 \
      --replication-factor 3; then
      return 0
    fi

    echo "Attempt ${attempt}/${MAX_TOPIC_ATTEMPTS} to create topic ${topic} failed; the cluster may still be forming quorum. Retrying in ${RETRY_DELAY_SECONDS}s..." >&2
    sleep "${RETRY_DELAY_SECONDS}"
    attempt=$((attempt + 1))
  done

  echo "Giving up creating topic ${topic} after ${MAX_TOPIC_ATTEMPTS} attempts." >&2
  return 1
}

for topic in "${TOPICS[@]}"; do
  echo "Ensuring topic ${topic} exists (RF=3)..."
  create_topic_with_retry "${topic}"
done

echo "Kafka topics ready."
