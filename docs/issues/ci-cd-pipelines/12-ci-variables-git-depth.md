# CI variables + `GIT_DEPTH` materialization

## Metadata

- ID: ci-12
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Inject **CI Variables** into the job environment: immutable predefined `CI_*` metadata (project, ref, SHA, job name, etc.) plus user `variables:` from pipeline defaults and job overrides. Implement **GIT_DEPTH** workspace materialization on the host: `0` mounts a full worktree; `N > 0` fetches a shallow archive. Reserved names cannot be overridden by authors.

## Acceptance criteria

- [ ] Predefined `CI_*` variables are set correctly for each job execution
- [ ] User `variables:` merge with pipeline defaults; job overrides win
- [ ] Attempts to override reserved `CI_*` names are rejected at parse or schedule time
- [ ] `GIT_DEPTH: 0` provides full repository worktree at `$CI_PROJECT_DIR`
- [ ] `GIT_DEPTH: N` (N > 0) provides shallow tree/archive sufficient for typical builds
- [ ] Scripts can read variables inside the MicroVM environment
- [ ] Integration test: job logs echo expected `CI_COMMIT_SHA` and custom variable

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)

## User stories covered

- 11 — Predefined immutable `CI_*` variables.
- 12 — Custom `variables:` for non-reserved names.
- 13 — `GIT_DEPTH` controls clone depth.

## Notes

- Repo fetch uses Job Identity scoped to the job SHA.
- Shallow archive format is an implementation detail; behavior must match PRD semantics.
