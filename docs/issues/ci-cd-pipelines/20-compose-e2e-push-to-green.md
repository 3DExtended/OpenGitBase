<!-- forge: #53 -->

# Compose E2E: push → green pipeline

## Metadata

- ID: ci-20
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Add an automated end-to-end test in the Docker Compose stack that exercises the full v1 happy path: push a commit with a multi-stage `.opengitbase-ci.yml`, trigger a **Pipeline Run** on `ogb-hosted`, pass all stages, and verify run status and logs via API (and optionally Playwright). This guards the critical tracer path and stage orchestration together.

## Acceptance criteria

- [ ] Test fixture repository includes `.opengitbase-ci.yml` with at least two sequential stages
- [ ] Git push (or simulated ingest) triggers pipeline run in compose environment
- [ ] All jobs complete with `passed` on platform compute
- [ ] API assertions on run status, job count, and log presence
- [ ] Test runs in CI or documented local command (`docker compose` + test runner)
- [ ] Failure output identifies which stage/job broke

## Blocked by

- [11-staged-pipelines-only-globs.md](./11-staged-pipelines-only-globs.md) (ci-11)
- [19-pipeline-ui-detail-logs.md](./19-pipeline-ui-detail-logs.md) (ci-19)

## User stories covered

- 21 — Push triggers **Pipeline Run**.
- 27 — Structured logs verifiable end-to-end.
- 9 — Sequential stages demonstrated in E2E fixture.

## Notes

- Optional Playwright layer can assert pipelines UI; API-level test is minimum bar.
- Hybrid `runs-on` and org nodes may get separate E2E fixtures later.
- Deferred v1 stories (MR pipelines, secrets, artifacts) are out of scope.
