<!-- forge: #52 -->

# Pipeline UI: detail, logs, cancel, commit badge

## Metadata

- ID: ci-19
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Complete the **Pipelines UI**: run detail page with per-job status and live log stream, cancel button for authorized users, commit page badge linking to the run for that SHA, and visibility rules — public repo logs world-readable, private repo logs members-only.

## Acceptance criteria

- [ ] Run detail page shows stages, jobs, statuses, and durations
- [ ] Job log view streams or polls logs during execution
- [ ] Cancel button visible to users with repository write access on running jobs
- [ ] Commit page shows CI status/link for the commit SHA when a run exists
- [ ] Public repositories: unauthenticated visitors can read pipeline logs
- [ ] Private repositories: only members can read pipeline logs
- [ ] UI matches structured log sections from the executor (layer, install, workspace, script)

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)
- [04-pipeline-run-api-empty-state.md](./04-pipeline-run-api-empty-state.md) (ci-04)

## User stories covered

- 26 — Job logs streamed during execution.
- 27 — Structured log sections.
- 29 — Cancel a running job (UI).
- 30 — Commit pages link to the run for that SHA.
- 32 — Public repo logs world-readable.
- 33 — Private repo logs members-only.

## Notes

- List/history shell from ci-04; this slice adds detail and commit integration.
- Cancel API from ci-18; UI can ship alongside or after API is ready.
