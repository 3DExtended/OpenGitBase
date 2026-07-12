# Firecracker `ogb-hosted` tracer

## Metadata

- ID: ci-prd-07
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (replaces process-sandbox ci-10 tracer)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Re-establish the **critical tracer bullet** on real infrastructure: push with `.opengitbase-ci.yml` → `ogb-hosted` job claimed by platform node → OverlayFS + Firecracker sandbox → workspace at `$CI_PROJECT_DIR` → `script` executes → structured logs → MicroVM teardown and Job Identity revocation → run passes.

This replaces the current process-sandbox dev path as the authoritative `ogb-hosted` execution mode when KVM is available.

## Acceptance criteria

- [ ] Single-job `ogb-hosted` pipeline passes end-to-end on platform compute node with Firecracker
- [ ] Repository workspace materialized at `$CI_PROJECT_DIR` for job SHA
- [ ] Logs stream during execution with sections: layer, workspace, script (install when dependencies present)
- [ ] Conservative `ogb-hosted` defaults enforced: 1 vCPU, 2 GiB RAM, 30 min timeout, 20 GiB ephemeral disk
- [ ] Job Identity revoked and MicroVM destroyed on success and failure
- [ ] Demo documented: push fixture YAML → green run in compose (with KVM) or documented bare-metal steps

## Blocked by

- [ci-prd-06-overlayfs-stack-assembly.md](./ci-prd-06-overlayfs-stack-assembly.md)

## User stories covered

- 7 — `runs-on: ogb-hosted` on platform nodes
- 24 — Failed job fails stage
- 26 — Job logs streamed during execution
- 27 — Structured log sections
- 53 — Conservative `ogb-hosted` defaults

## Notes

- ci-10 marked complete with `ProcessSandboxExecutor`; this slice fulfills ci-10 acceptance criteria as originally written.
