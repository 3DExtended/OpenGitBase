# Pipeline run read API + empty state

## Metadata

- ID: ci-04
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Expose read APIs for **Pipeline Runs** and add the repository **Pipelines** tab shell. When a repository has no CI configuration (no runs and no `.opengitbase-ci.yml` at default branch or equivalent check), show the **CI Not Configured** empty state with guidance linking to user docs. When runs exist, list them with status, ref, SHA, and timestamps.

## Acceptance criteria

- [ ] Authenticated API returns paginated pipeline runs for `/{owner}/{repo}`
- [ ] Run list includes ref, commit SHA, overall status, and created timestamp
- [ ] Web route `/{owner}/{repo}/pipelines` renders run history when runs exist
- [ ] Empty state displays when CI is not configured, with link to `/docs/ci` guidance
- [ ] Public repos allow unauthenticated read of run list metadata (log detail deferred to ci-19)
- [ ] Unit or integration test covers empty state vs populated list

## Blocked by

- [03-push-trigger-pipeline-run.md](./03-push-trigger-pipeline-run.md) (ci-03)

## User stories covered

- 31 — Pipelines tab on the repository.
- 34 — Empty state with guidance when CI is not configured.

## Notes

- Commit page linkage and log streaming land in ci-19.
- Reuse existing repository access checks for private vs public visibility.
