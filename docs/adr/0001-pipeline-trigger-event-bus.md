# ADR 0001: Pipeline triggers via storage hook and event bus

## Status

Accepted (2026-07-11)

## Context

CI/CD pipelines must start when a user pushes commits. OpenGitBase already uses storage `post-receive` hooks to call the API for push validation and quorum replication. Pipeline scheduling is a separate concern that will gain more consumers later (notifications, audit, future MR pipelines).

## Decision

1. The primary storage `post-receive` hook calls a thin internal API endpoint after a successful receive, passing `repositoryId`, `ref`, and `afterSha`.
2. The API publishes a `GitPushReceived` event to Kafka (topic `git.push.received`).
3. A pipeline scheduler consumer (consumer group `pipeline-scheduler`) handles the event, loads `.opengitbase-ci.yml` at `afterSha`, and creates a **Pipeline Run**.

Storage nodes do not parse pipeline YAML. The hook only reports facts about the push.

## Consequences

- New git-side features can subscribe to the same event without extending `post-receive` shell scripts.
- Pipeline scheduling retries and observability live in the API tier.
- Event ordering and idempotency must be handled in the scheduler (duplicate events for the same `afterSha` should not create duplicate runs).
- Kafka becomes required infrastructure for CI triggers; docker-compose includes **three Kafka brokers** (KRaft) with topic replication factor 3, consistent with the project's HA storage posture.
- MR-triggered pipelines will likely publish a different event later rather than overloading the storage hook.
