# Vsock in-guest install + script execution

## Metadata

- ID: ci-rt-08
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Define and implement the vsock guest agent protocol: host sends execute requests with `{user, cwd, script, env}`; guest streams stdout/stderr lines back. Run ordered `installscript` phases as root inside the guest; run `script` as **Job Execution User** `ogb` by default or `root` when YAML specifies `user: root`.

Integrate with **Job Sandbox** orchestration so layer prep, workspace mount, install, and script phases produce structured log sections.

## Acceptance criteria

- [ ] `installscript` executes as root inside the guest MicroVM
- [ ] `script` executes as `ogb` by default inside the guest
- [ ] YAML `user: root` runs `script` as root inside the guest
- [ ] Vsock protocol streams stdout/stderr lines to the host during execution
- [ ] End-to-end tracer: push → claim → in-guest `script` echo → job passes on KVM host

## Blocked by

- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)
- [ci-rt-07-virtiofs-workspace-guest.md](./ci-rt-07-virtiofs-workspace-guest.md) (ci-rt-07)

## User stories covered

- 2 — `installscript` as root inside guest
- 3 — `script` as `ogb` by default
- 4 — Optional `user: root` for script phase

## Notes

- Incremental log forwarding to API is ci-rt-12; this slice streams host-side from vsock.
- Install fail-fast behavior is ci-rt-04.
