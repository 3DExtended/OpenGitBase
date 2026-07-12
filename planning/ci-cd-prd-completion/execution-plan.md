# CI/CD PRD completion — execution plan

**PRD:** `docs/prd/ci-cd-pipelines.md`  
**Work items:** `planning/ci-cd-prd-completion/items/`  
**Branch strategy:** **main** (all work items committed sequentially on default branch).

## Topological order

1. ci-prd-01 — Platform compute bootstrap + admin fleet UI
2. ci-prd-02 — Org compute settings UI + enrollment API hardening
3. ci-prd-04 — Base image catalog build + Layer Store artifacts
4. ci-prd-05 — Firecracker MicroVM executor + operator requirements
5. ci-prd-10 — Job Identity security contract tests
6. ci-prd-13 — Pipeline log visibility + live streaming UI
7. ci-prd-03 — Org compute enroll → job routing integration test
8. ci-prd-06 — OverlayFS stack assembly in compute agent
9. ci-prd-12 — Admin + org domain allowance review UI
10. ci-prd-07 — Firecracker `ogb-hosted` tracer
11. ci-prd-08 — Layer promotion runtime + promoted layer mount
12. ci-prd-09 — Host egress enforcement in compute agent
13. ci-prd-11 — Admin CI console: base images + promotion dashboard
14. ci-prd-14 — Compose bootstrap E2E gate
15. ci-prd-15 — Community-hosted hybrid tracer

## Status

| ID | Title | Status |
|----|-------|--------|
| ci-prd-01 | Platform compute bootstrap + admin fleet UI | completed |
| ci-prd-02 | Org compute settings UI + enrollment API hardening | completed |
| ci-prd-03 | Org compute enroll → job routing integration test | pending |
| ci-prd-04 | Base image catalog build + Layer Store artifacts | pending |
| ci-prd-05 | Firecracker MicroVM executor + operator requirements | pending |
| ci-prd-06 | OverlayFS stack assembly in compute agent | pending |
| ci-prd-07 | Firecracker `ogb-hosted` tracer | pending |
| ci-prd-08 | Layer promotion runtime + promoted layer mount | pending |
| ci-prd-09 | Host egress enforcement in compute agent | pending |
| ci-prd-10 | Job Identity security contract tests | pending |
| ci-prd-11 | Admin CI console: base images + promotion dashboard | pending |
| ci-prd-12 | Admin + org domain allowance review UI | pending |
| ci-prd-13 | Pipeline log visibility + live streaming UI | pending |
| ci-prd-14 | Compose bootstrap E2E gate | pending |
| ci-prd-15 | Community-hosted hybrid tracer | pending |
