# Compute agent runtime

## Metadata

- ID: ci-08
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Ship the `opengitbase-compute-agent` service skeleton: enroll with a token, establish **Node Identity**, send heartbeats, and long-poll the claim API in a loop. On claim, acknowledge the job and report completion status back to the API (stub executor acceptable — real Firecracker lands in ci-10). Org agents use HTTPS long-poll only; no Kafka consumer required for org nodes.

## Acceptance criteria

- [ ] Agent binary/service enrolls with enrollment token and persists Node Identity
- [ ] Heartbeat loop keeps node marked healthy in registry
- [ ] Long-poll claim loop receives jobs matching node's hosting scope
- [ ] Agent reports `running`, then `passed` or `failed` with minimal stub execution
- [ ] Agent runs in Docker Compose alongside a platform compute node profile
- [ ] Org-scoped agent can enroll via org token without Kafka connectivity
- [ ] Integration test: enrolled agent claims and completes a stub job end-to-end

## Blocked by

- [07-job-queue-claim-job-identity.md](./07-job-queue-claim-job-identity.md) (ci-07)

## User stories covered

- 41 — Org agents claim work via HTTPS long-poll only.

## Notes

- Kafka consumer for platform agents is ci-17; this slice uses long-poll for all agents first.
- Firecracker sandbox, workspace, and logs replace the stub in ci-10.
