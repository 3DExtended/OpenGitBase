# CI/CD Pipelines — progress log

Started: 2026-07-11

## ci-01 — Compose foundation: Kafka + MinIO

**Status:** complete

## ci-02 .. ci-18

**Status:** complete

All foundational backend and compose work items through ci-18 are implemented and validated in the branch history.

## ci-04 — Pipeline run read API + empty state

**Status:** complete

- `GET /pipeline/runs/{runId}` now returns run metadata and ordered job list.
- Added `PipelineJobLogEntity` plus `GET /pipeline/jobs/{jobId}/logs`.
- Job status updates now append structured log lines (`section`, `line`, `timestamp`).
- Added repository pipelines list UI with CI empty state and docs link.

## ci-19 — Pipeline UI detail, logs, cancel, commit badge

**Status:** complete

- Added run detail page with jobs, status badges, cancel action, and polled logs.
- Added pipelines nav item and i18n strings.
- Added commit page pipeline badge linking to matching run.
- Added visual gallery fixture and `tests/visual/pipelines.spec.ts`.

## ci-20 — Compose E2E push -> green pipeline

**Status:** complete

- Added `scripts/test-pipelines-e2e.sh` to exercise health check, fixture push, run completion, job count, and log assertions via API.

## Overall

All ci-01..ci-20 work items are complete.
