# Layer promotion jobs + real overlay deltas

## Metadata

- ID: ci-rt-11
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Replace the inline text-blob promotion worker with **Layer Promotion** as a dedicated internal **Job** on a **Platform Compute Node**: after admin approval and five-success eligibility, schedule promotion work that runs the recipe `installscript` in a clean base **Job Sandbox**, captures the OverlayFS upper-layer delta from base only, and uploads the content-addressed artifact to **Layer Store**.

Subsequent jobs with matching PRD recipe keys mount the promoted layer and log cache hits.

## Acceptance criteria

- [ ] Admin promotion request enqueues internal `ogb-hosted` promotion job (not inline API worker blob)
- [ ] Promotion job runs only on **Platform Compute Nodes**
- [ ] Uploaded artifact is a real filesystem delta, not a text placeholder
- [ ] Subsequent job logs promoted layer cache hit and skips live installscript
- [ ] Promotion remains blocked when last five **Dependency Install Outcomes** are not all successful

## Blocked by

- [ci-rt-04-recipe-keys-install-failfast.md](./ci-rt-04-recipe-keys-install-failfast.md) (ci-rt-04)
- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)
- [ci-rt-08-vsock-in-guest-execution.md](./ci-rt-08-vsock-in-guest-execution.md) (ci-rt-08)

## User stories covered

- 30 — **Layer Promotion** as dedicated internal platform job
- 31 — Promoted layers stored as real OverlayFS deltas in **Layer Store**
- 32 — Promotion blocked unless last five installs succeeded

## Notes

- Chained recipe promotion (ordered dependencies depending on prior recipes) remains out of scope per parent PRD.
