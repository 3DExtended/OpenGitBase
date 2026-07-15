<!-- forge: #50 -->

# Platform agent Kafka job wake

## Metadata

- ID: ci-17
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Add a Kafka consumer to **Platform Compute Node Agents** on `ci.job.available` so platform nodes wake promptly when jobs enqueue, while still claiming exclusively via the HTTPS API. Org agents continue long-poll only. Consumer offsets and reconnect behavior must be robust for the three-broker cluster.

## Acceptance criteria

- [ ] Platform agents subscribe to `ci.job.available` on enrollment
- [ ] Kafka notification triggers immediate claim attempt (not only periodic long-poll)
- [ ] Claim still goes through API for atomic assignment and Job Identity minting
- [ ] Org agents do not require Kafka connectivity
- [ ] Consumer survives broker rolling restart in compose
- [ ] Measurable lower queue latency for `ogb-hosted` vs long-poll-only baseline

## Blocked by

- [08-compute-agent-runtime.md](./08-compute-agent-runtime.md) (ci-08)
- [07-job-queue-claim-job-identity.md](./07-job-queue-claim-job-identity.md) (ci-07)

## User stories covered

- 45 — Platform agents use Kafka job notifications.

## Notes

- Org long-poll remains the sole wake path for `organization-self-hosted` and `community-hosted` agents (story 41).
- Duplicate notifications are safe because claim is idempotent at the API.
