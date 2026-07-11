# CI/CD Pipelines — execution plan

**PRD:** `docs/prd/ci-cd-pipelines.md`  
**Work items:** `docs/issues/ci-cd-pipelines/`  
**Branch strategy:** **main** (all work items committed sequentially on default branch).

## Topological order

1. ci-01 — Compose foundation: Kafka + MinIO
2. ci-02 — Pipeline YAML parser + v1 validation
3. ci-03 — Push trigger → Pipeline Run (no execution) — blocked by ci-01, ci-02
4. ci-04 — Pipeline run read API + empty state — blocked by ci-03
5. ci-05 — Compute node registry + platform enrollment — blocked by ci-01
6. ci-06 — Org Owner self-service compute enrollment — blocked by ci-05
7. ci-07 — Job queue, claim API, Job Identity — blocked by ci-03, ci-05
8. ci-08 — Compute agent runtime — blocked by ci-07
9. ci-09 — Base Image Catalog + Layer Store seed — blocked by ci-01, ci-05
10. ci-10 — Tracer: first `ogb-hosted` job end-to-end — blocked by ci-07, ci-08, ci-09
11. ci-11 — Staged pipelines + `only` globs — blocked by ci-10
12. ci-12 — CI variables + `GIT_DEPTH` materialization — blocked by ci-10
13. ci-13 — Dependency live install + telemetry — blocked by ci-10
14. ci-14 — Layer promotion admin + promoted mounts — blocked by ci-13, ci-09
15. ci-15 — Hybrid `runs-on` routing — blocked by ci-10, ci-06
16. ci-16 — Egress allowlists + domain requests — blocked by ci-10, ci-06
17. ci-17 — Platform agent Kafka job wake — blocked by ci-08, ci-07
18. ci-18 — Job timeout, cancel, resource limits — blocked by ci-10
19. ci-19 — Pipeline UI: detail, logs, cancel, commit badge — blocked by ci-10, ci-04
20. ci-20 — Compose E2E: push → green pipeline — blocked by ci-11, ci-19

## Dependency graph

```
ci-01 ─┬→ ci-03 ─→ ci-04 ────────────────┐
       │         ↘                         │
ci-02 ─┘           ci-07 ─→ ci-08 ─┐       │
ci-01 ─→ ci-05 ─→ ci-06 ───────────┼→ ci-10 ─┬→ ci-11 ─┐
       │         ↘                 │         ├→ ci-12  │
ci-01 ─┴→ ci-09 ───────────────────┘         ├→ ci-13 ─→ ci-14
                                              ├→ ci-15
                                              ├→ ci-16
                                              ├→ ci-18
                                              └→ ci-19 ─→ ci-20
ci-07, ci-08 → ci-17
```

## Status

| ID | Title | Status |
|----|-------|--------|
| ci-01 | Compose foundation: Kafka + MinIO | complete |
| ci-02 | Pipeline YAML parser + v1 validation | complete |
| ci-03 | Push trigger → Pipeline Run | complete |
| ci-04 | Pipeline run read API + empty state | complete |
| ci-05 | Compute node registry + platform enrollment | complete |
| ci-06 | Org Owner self-service compute enrollment | complete |
| ci-07 | Job queue, claim API, Job Identity | complete |
| ci-08 | Compute agent runtime | complete |
| ci-09 | Base Image Catalog + Layer Store seed | complete |
| ci-10 | Tracer: first ogb-hosted job end-to-end | complete |
| ci-11 | Staged pipelines + only globs | complete |
| ci-12 | CI variables + GIT_DEPTH materialization | complete |
| ci-13 | Dependency live install + telemetry | complete |
| ci-14 | Layer promotion admin + promoted mounts | complete |
| ci-15 | Hybrid runs-on routing | complete |
| ci-16 | Egress allowlists + domain requests | complete |
| ci-17 | Platform agent Kafka job wake | complete |
| ci-18 | Job timeout, cancel, resource limits | complete |
| ci-19 | Pipeline UI: detail, logs, cancel, commit badge | complete |
| ci-20 | Compose E2E: push → green pipeline | complete |
