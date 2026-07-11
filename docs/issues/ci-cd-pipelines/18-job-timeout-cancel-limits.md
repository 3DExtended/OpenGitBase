# Job timeout, cancel, resource limits

## Metadata

- ID: ci-18
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Enforce **Job Resource Limits** and lifecycle controls: automatic timeout (30 min default for `ogb-hosted`), user-initiated cancel for authors with repository write access, and hard enforcement of CPU/memory/disk ceilings declared at node enrollment. Cancel and timeout tear down the MicroVM, revoke Job Identity, and mark the job `cancelled` or `failed` appropriately.

## Acceptance criteria

- [ ] `ogb-hosted` jobs killed after 30 minutes unless overridden in v1 policy table
- [ ] User with repo write access can cancel a running job via API
- [ ] Cancel propagates to agent; MicroVM destroyed and Job Identity revoked
- [ ] Node refuses new claims when at `MaxConcurrentJobs` or resource capacity
- [ ] Running job exceeding memory/CPU policy is terminated with logged reason
- [ ] Timeout and cancel reflected in run and stage status
- [ ] Integration test: cancel mid-run stops execution; timeout fires on sleep job

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)

## User stories covered

- 28 — Automatic job timeout.
- 29 — Cancel a running job with push access.
- 40 — Capacity reductions rejected while jobs exceed limits (enforcement side).
- 53 — Conservative `ogb-hosted` defaults.

## Notes

- Capacity update rejection at registry is ci-05/ci-06; this slice enforces at runtime.
- Cancel authorization reuses repository write permission model.
