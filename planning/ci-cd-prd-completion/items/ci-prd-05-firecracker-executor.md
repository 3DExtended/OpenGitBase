# Firecracker MicroVM executor + operator requirements

## Metadata

- ID: ci-prd-05
- Type: HITL + AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-10)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Replace the dev-only **ProcessSandboxExecutor** with a production **Firecracker MicroVM** execution path behind the existing `ISandboxExecutor` abstraction. Document operator requirements for KVM, `/dev/kvm`, and compose privileged mode.

**HITL deliverable:** short ADR or operator doc deciding compose posture (privileged agent container vs bare-metal agent vs dev fallback).

**AFK deliverable:** `FirecrackerSandboxExecutor` that boots a MicroVM per job, runs `script` as `ogb` by default or `root` when specified, and tears down VM on completion/cancel/timeout.

## Acceptance criteria

- [ ] ADR or operator doc records KVM/Firecracker requirements and dev fallback policy
- [ ] `FirecrackerSandboxExecutor` implements `ISandboxExecutor`
- [ ] Agent selects Firecracker when KVM available; documents when `PreferProcessSandbox` remains valid
- [ ] Guest execution honors `user: root` vs default `ogb` job setting
- [ ] VM destroyed on job completion, failure, cancel, and timeout
- [ ] Unit or integration test with mocked Firecracker boundary, plus manual compose verification note

## Blocked by

- None — can start immediately

## User stories covered

- 19 — `script` runs as `ogb` by default
- 20 — Optional `user: root`
- 54 — Firecracker per job isolation

## Notes

- `ProcessSandboxExecutor` stays as explicit dev fallback per compute agent README.
- ci-10 tracer used process sandbox intentionally; this slice fulfills the PRD isolation model.
