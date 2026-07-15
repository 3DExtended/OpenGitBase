<!-- forge: #36 -->

# Push trigger → Pipeline Run (no execution)

## Metadata

- ID: ci-03
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Wire the end-to-end trigger path from git push to **Pipeline Run** creation without executing jobs yet. Storage `post-receive` calls a thin internal **Git Push Ingestion** API; the API publishes **Git Push Received** to Kafka. The **Pipeline Scheduler** consumer loads `.opengitbase-ci.yml` at `afterSha`, parses it, and creates a **Pipeline Run** with jobs in `queued` state. Missing CI file is a silent no-op. Duplicate `repositoryId + afterSha` does not create a second run.

## Acceptance criteria

- [ ] Storage hook posts `repositoryId`, `ref`, `afterSha` to the internal ingest endpoint
- [ ] Ingest publishes `git.push.received` to Kafka with validated caller trust
- [ ] Scheduler consumer creates a **Pipeline Run** when CI file exists at the commit SHA
- [ ] Push without `.opengitbase-ci.yml` creates no run and emits no user-visible error
- [ ] Duplicate events for the same commit do not create duplicate runs (idempotent)
- [ ] Jobs are persisted in Postgres as `queued` but not yet claimed or executed
- [ ] Integration test: push with CI file → run row exists; push without → no row

## Blocked by

- [01-compose-kafka-minio-foundation.md](./01-compose-kafka-minio-foundation.md) (ci-01)
- [02-pipeline-yaml-parser.md](./02-pipeline-yaml-parser.md) (ci-02)

## User stories covered

- 21 — Push to `main` triggers a **Pipeline Run**.
- 22 — No run when CI file is absent.
- 63 — Storage hooks only report push metadata.
- 65 — Idempotent pipeline creation per commit.

## Notes

- Job enqueue and stage advancement logic may be minimal here; full stage orchestration hardens in ci-07 and ci-11.
- Scheduler fetches CI file using existing repository read APIs at the pinned SHA.
