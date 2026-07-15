<!-- forge: #38 -->

# Compute node registry + platform enrollment

## Metadata

- ID: ci-05
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Introduce the **Compute Node Registry** for platform-admin enrollment of **Platform Compute Nodes**. Platform admins create enrollment tokens; agents register and receive **Node Identity**. Enrollment requires **Compute Node Capacity** (`MaxConcurrentJobs`, `MaxCpu`, `MaxMemoryBytes`). Heartbeats update utilization. Capacity updates are rejected when running jobs exceed new limits. Mirrors storage-node enrollment patterns.

## Acceptance criteria

- [ ] Postgres schema for compute nodes, capacity, hosting scope, and enrollment tokens
- [ ] Platform admin API creates enrollment tokens for platform nodes
- [ ] Agent registration exchanges token for `nodeId` and long-lived **Node Identity**
- [ ] Heartbeat endpoint marks nodes healthy and reports utilization
- [ ] Enrollment rejects missing capacity fields
- [ ] Capacity reduction rejected while `runningJobs > newMaxConcurrentJobs` or equivalent resource breach
- [ ] Admin UI or API lists registered platform nodes with health status
- [ ] Integration test: enroll → register → heartbeat → visible as eligible for `ogb-hosted`

## Blocked by

- [01-compose-kafka-minio-foundation.md](./01-compose-kafka-minio-foundation.md) (ci-01)

## User stories covered

- 38 — Require `MaxConcurrentJobs`, `MaxCpu`, and `MaxMemoryBytes` at enrollment.
- 39 — Update capacity later.
- 40 — Capacity reductions rejected while jobs exceed new limits.
- 44 — Platform admin enrolls **Platform Compute Nodes**.

## Notes

- Org self-service enrollment is ci-06.
- Node Identity must not grant arbitrary repository read (story 60) — scope to node operations only.
