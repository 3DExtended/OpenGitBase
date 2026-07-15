<!-- forge: #40 -->

# Job queue, claim API, Job Identity

## Metadata

- ID: ci-07
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Implement the **Job Queue and Lifecycle** module with Postgres as state of record. When the scheduler creates jobs, enqueue them and publish `ci.job.available` to Kafka. Provide a **Claim Job** API: agents authenticate with **Node Identity**, pass eligible hosting profiles, and receive a claimed job spec plus freshly minted **Job Identity**. Job Identity is scoped to one job execution (repo read at SHA, log write, layer store read, status update) and is distinct from Node Identity.

## Acceptance criteria

- [ ] Jobs transition `queued → running → passed|failed|cancelled` with audit logging
- [ ] Scheduler publishes `ci.job.available` when jobs become claimable
- [ ] `ClaimJob(nodeId, hostingProfiles[])` atomically assigns one eligible queued job or returns empty for long-poll
- [ ] Claim response includes resolved job spec and minted **Job Identity** credential
- [ ] **Job Identity Service** mints and revokes credentials; Node Identity cannot read arbitrary repos
- [ ] Job status transitions are logged for operations (queued, running, passed, failed, cancelled)
- [ ] Integration test: enqueue → claim → status update → identity revoked on completion stub

## Blocked by

- [03-push-trigger-pipeline-run.md](./03-push-trigger-pipeline-run.md) (ci-03)
- [05-compute-node-registry-platform-enrollment.md](./05-compute-node-registry-platform-enrollment.md) (ci-05)

## User stories covered

- 23 — Skipped jobs (non-matching `only`) omitted from the run.
- 57 — Per-job credentials for repo access.
- 58 — Job credentials scoped to one SHA.
- 59 — Job credentials expire at teardown.
- 60 — Node credentials cannot read arbitrary repos.
- 66 — Postgres as job state of record.
- 67 — Job status transitions logged.

## Notes

- Stage orchestration (parallel within stage, skip later stages on failure) hardens in ci-11; basic enqueue from ci-03 runs through here.
- Actual sandbox execution uses Job Identity in ci-10.
