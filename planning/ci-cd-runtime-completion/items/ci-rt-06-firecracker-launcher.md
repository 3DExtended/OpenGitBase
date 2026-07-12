# Real Firecracker launcher + VM lifecycle

## Metadata

- ID: ci-rt-06
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Implement production **Firecracker Launcher** behavior: when KVM and Firecracker binary are available and `PreferProcessSandbox` is false, boot one MicroVM per **Job** using the composed OverlayFS rootfs, platform-pinned kernel, and tap interface. Destroy the MicroVM on completion, failure, timeout, and cancel (cancel integration completed in ci-rt-13).

First tracer: run a minimal command inside the guest via vsock and verify the VM is gone afterward. Process sandbox remains the compose default per ADR 0002.

## Acceptance criteria

- [ ] Firecracker binary invoked to boot a real MicroVM when KVM is present
- [ ] OverlayFS merged root used as guest rootfs (not host `sh -c` fallback on production path)
- [ ] MicroVM destroyed on job completion and failure
- [ ] `PreferProcessSandbox: true` still selects process executor for compose
- [ ] Unit tests at launcher boundary; manual or integration note for KVM verification

## Blocked by

- [ci-rt-05-base-image-guest-agent.md](./ci-rt-05-base-image-guest-agent.md) (ci-rt-05)

## User stories covered

- 1 — Firecracker MicroVM per job
- 5 — Compose keeps process sandbox fallback
- 9 — MicroVM destroyed on completion, failure, cancel, and timeout

## Notes

- In-guest script execution and workspace mount are ci-rt-07 and ci-rt-08.
- This slice replaces the current launcher that delegates to host shell even when KVM exists.
