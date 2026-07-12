# OverlayFS stack assembly in compute agent

## Metadata

- ID: ci-prd-06
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-10)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Implement **OverlayFS stack composition** in the compute agent before MicroVM boot: base image layer (bottom), optional dependency layers, and ephemeral writable upper layer. Emit structured log output for the layer-mount phase so authors can distinguish mount failures from script failures.

## Acceptance criteria

- [ ] Agent resolves base image artifact from catalog slug and mounts as bottom OverlayFS layer
- [ ] Promoted dependency layers (when present) mount in declared order above base image
- [ ] Ephemeral upper layer created per job and discarded on teardown
- [ ] Composed rootfs passed to Firecracker as guest root (or documented mount contract)
- [ ] Logs include `layer` section with mount success/failure per layer
- [ ] Integration test or agent test proves stack assembly with at least base + upper layers

## Blocked by

- [ci-prd-04-base-image-build-layer-store.md](./ci-prd-04-base-image-build-layer-store.md)
- [ci-prd-05-firecracker-executor.md](./ci-prd-05-firecracker-executor.md)

## User stories covered

- 16 — Promoted layers used automatically when available (mount path only; promotion build is ci-prd-08)
- 17 — Logs show layer hit vs live install
- 54 — Firecracker + OverlayFS per job

## Notes

- Dependency live install (ci-13) runs inside guest after stack is booted; this slice is host-side composition only.
