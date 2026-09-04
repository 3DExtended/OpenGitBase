#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
#
# Kafka broker healthcheck that verifies the KRaft controller WRITE path, not
# just read liveness.
#
# The previous healthcheck ran `kafka-broker-api-versions.sh`, which succeeds as
# long as a broker answers reads. But during the #229 incident the KRaft
# controller quorum was in a split-vote deadlock: reads worked while every
# metadata write (createTopics, broker registration) timed out, and all three
# brokers still reported "healthy". The write-path failure was invisible.
#
# This check asserts the controller quorum has an elected leader (LeaderId is a
# non-negative integer). No leader ⇒ no metadata can be committed ⇒ unhealthy.
# Reaching the quorum tool at all also implies basic broker reachability, so this
# subsumes the old liveness probe. Transient elections are absorbed by the
# healthcheck's retry count, so only a persistent "no leader" trips it.

set -uo pipefail

BOOTSTRAP="${KAFKA_HEALTHCHECK_BOOTSTRAP:-kafka-1:29092,kafka-2:29092,kafka-3:29092}"

status="$(/opt/kafka/bin/kafka-metadata-quorum.sh \
  --bootstrap-server "${BOOTSTRAP}" describe --status 2>/dev/null)" || exit 1

leader="$(printf '%s\n' "${status}" \
  | awk -F: '/LeaderId/ { gsub(/[ \t]/, "", $2); print $2; exit }')"

# Empty, "-1", or any non-digit value means no leader has been elected.
case "${leader}" in
  '' | *[!0-9]*) exit 1 ;;
  *) exit 0 ;;
esac
