# Compose Firecracker profile

## Metadata

- ID: ci-rt-15
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Add an optional Docker Compose profile for local Firecracker development on Linux hosts with KVM: privileged compute agent service, `/dev/kvm` device mount, Firecracker binary in agent image, `PreferProcessSandbox: false` under the profile. Default compose stack unchanged (process sandbox).

Document profile usage in operator docs alongside ADR 0002.

## Acceptance criteria

- [ ] `docker compose --profile firecracker` starts agent configured for MicroVM path
- [ ] Default `docker compose up` still uses `PreferProcessSandbox: true`
- [ ] Profile documented with KVM and privileged requirements
- [ ] Agent image includes Firecracker binary when built for profile
- [ ] Manual verification steps documented for developers without mandatory CI KVM

## Blocked by

- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)

## User stories covered

- 7 — Optional compose Firecracker profile for Linux developers

## Notes

- Does not replace bare-metal E2E gate (ci-rt-16).
- macOS developers continue using process sandbox only.
