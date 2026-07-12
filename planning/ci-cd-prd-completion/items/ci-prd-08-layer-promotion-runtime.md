# Layer promotion runtime + promoted layer mount

## Metadata

- ID: ci-prd-08
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-14)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Turn **Layer Promotion** from a queued database record into a real workflow: after five consecutive successful live installs for a recipe, a platform-only promotion build produces a **Dependency Layer** artifact in the Layer Store; subsequent jobs mount it in the OverlayFS stack and skip `installscript` execution.

## Acceptance criteria

- [ ] Admin promotion request triggers promotion build job on platform compute node only
- [ ] Promotion blocked unless last five install outcomes for recipe succeeded
- [ ] Promotion build uploads layer artifact to Layer Store with content hash
- [ ] Subsequent jobs with matching recipe mount promoted layer (log evidences cache hit)
- [ ] Failed promotion does not publish layer; streak must recover before retry
- [ ] Integration test: five successful installs → promote → sixth job skips live install
- [ ] Non-admin users cannot trigger promotion

## Blocked by

- [ci-prd-07-firecracker-ogb-hosted-tracer.md](./ci-prd-07-firecracker-ogb-hosted-tracer.md)

## User stories covered

- 16 — Promoted layers used automatically when available
- 17 — Logs show layer hit vs live install
- 48 — Dependency usage analytics for promotion decisions
- 49 — Promotion blocked unless last five installs succeeded
- 50 — Promotion builds only on platform nodes
- 51 — Layer Store + node caching
- 68 — Install outcomes recorded for evidence

## Notes

- `RequestDependencyLayerPromotionQueryHandler` today only inserts `Queued` row; wire to dispatcher/build executor.
- Org-admin layer promotion remains out of scope per PRD.
