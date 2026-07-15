<!-- forge: #43 -->

# Tracer: first `ogb-hosted` job end-to-end

## Metadata

- ID: ci-10
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Deliver the first real **Job Sandbox** execution on a **Platform Compute Node** for `runs-on: ogb-hosted`. The agent composes OverlayFS (base image bottom layer + ephemeral upper), boots a **Firecracker MicroVM**, materializes the repository workspace at `$CI_PROJECT_DIR` using **Job Identity**, runs the job `script` as `ogb` by default, streams structured logs (layer mount, workspace, script sections), and tears down the VM while revoking Job Identity. Apply conservative `ogb-hosted` defaults: 1 vCPU, 2 GiB RAM, 30 min timeout, 20 GiB ephemeral disk.

This is the critical tracer bullet — push → run → pass for a single-job pipeline.

## Acceptance criteria

- [ ] Agent boots Firecracker MicroVM per claimed job with OverlayFS root from catalog base image
- [ ] Repository workspace mounted or extracted at `$CI_PROJECT_DIR` for the job SHA
- [ ] Job `script` executes inside guest as `ogb` unless `user: root` is set
- [ ] Logs stream to API during execution with section markers
- [ ] MicroVM destroyed and Job Identity revoked on completion or failure
- [ ] `ogb-hosted` resource defaults enforced (CPU, memory, timeout, disk)
- [ ] Demo: push `.opengitbase-ci.yml` with one `ogb-hosted` job → run passes

## Blocked by

- [07-job-queue-claim-job-identity.md](./07-job-queue-claim-job-identity.md) (ci-07)
- [08-compute-agent-runtime.md](./08-compute-agent-runtime.md) (ci-08)
- [09-base-image-catalog-layer-store.md](./09-base-image-catalog-layer-store.md) (ci-09)

## User stories covered

- 7 — `runs-on: ogb-hosted` on platform nodes.
- 19 — `script` runs as `ogb` by default.
- 20 — Optional `user: root`.
- 24 — Failed job fails the stage (single-job case).
- 26 — Job logs streamed during execution.
- 27 — Structured log sections.
- 54 — Firecracker per job isolation.
- 53 — Conservative `ogb-hosted` defaults.

## Notes

- Dependencies with `installscript` are ci-13; this tracer may use a base image with tools preinstalled.
- `GIT_DEPTH` shallow vs full worktree nuances are ci-12.
- KVM/Firecracker availability in compose may require `privileged` or `/dev/kvm` mount — document operator requirements.
