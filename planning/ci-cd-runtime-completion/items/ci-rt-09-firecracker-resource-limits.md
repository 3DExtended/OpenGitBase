# Firecracker resource limits from job spec

## Metadata

- ID: ci-rt-09
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Map **Job** database fields (`CpuLimit`, `MemoryMiB`, `DiskGiB`, `TimeoutSeconds`) to Firecracker VM configuration at boot. Apply conservative `ogb-hosted` defaults already stored at schedule time. Host watchdog destroys the MicroVM when timeout elapses.

## Acceptance criteria

- [ ] `ogb-hosted` jobs boot MicroVM with 1 vCPU and 2 GiB RAM when defaults apply
- [ ] Timeout from job spec destroys VM and marks job failed if not already terminal
- [ ] Ephemeral disk / writable space bounded per job spec defaults (20 GiB for `ogb-hosted`)
- [ ] Contract or integration test verifies limit mapping from job row to FC config
- [ ] Process sandbox path unchanged for compose

## Blocked by

- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)

## User stories covered

- 8 — Conservative `ogb-hosted` CPU, RAM, disk, and timeout enforced

## Notes

- Cancel teardown timing is ci-rt-13; timeout teardown is in scope here.
