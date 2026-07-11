# Staged pipelines + `only` globs

## Metadata

- ID: ci-11
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Harden **Pipeline Scheduler** stage orchestration: jobs in the same stage run in parallel; stages run sequentially. Evaluate `only` globs so non-matching jobs are omitted from the run entirely. When any job in a stage fails, mark the stage failed, let in-flight siblings finish, and do not enqueue jobs in later stages.

## Acceptance criteria

- [ ] Jobs within one stage enqueue together and can run concurrently on multiple nodes
- [ ] Next stage jobs enqueue only after all jobs in the prior stage reach terminal state
- [ ] Jobs whose `only` pattern does not match the push ref are not created for the run
- [ ] Stage failure after a job fails prevents scheduling subsequent stages
- [ ] In-flight jobs in a failing stage continue to completion and log
- [ ] Run overall status reflects stage outcomes correctly
- [ ] Integration test: two-stage pipeline with intentional test failure skips stage two

## Blocked by

- [10-first-ogb-hosted-job-tracer.md](./10-first-ogb-hosted-job-tracer.md) (ci-10)

## User stories covered

- 7 — `only` glob filters.
- 8 — Parallel jobs within a stage.
- 9 — Sequential stages.
- 23 — Skipped jobs omitted from the run.
- 24 — Failed job fails stage and skips later stages.
- 25 — In-flight jobs in failing stage finish.

## Notes

- Mixed `runs-on` within one stage (story 10) is schedulable here; routing to correct node pools hardens in ci-15.
