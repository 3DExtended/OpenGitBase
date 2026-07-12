# Bare-metal Firecracker E2E gate

## Metadata

- ID: ci-rt-16
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Add `scripts/test-pipelines-firecracker-e2e.sh` (or equivalent) that runs on a KVM-enabled bare-metal or self-hosted runner: git push → **Pipeline Run** → real Firecracker **Job Sandbox** → in-guest script passes → VM destroyed. Document as mandatory runtime completion gate in release checklist.

Confirm existing `scripts/test-pipelines-e2e.sh` process-sandbox compose E2E still passes unchanged.

## Acceptance criteria

- [ ] Bare-metal E2E script passes on KVM host with `PreferProcessSandbox: false`
- [ ] Script verifies job reaches `Passed` with evidence of MicroVM path (not process sandbox fallback message only)
- [ ] Compose process-sandbox E2E remains green without KVM
- [ ] Release or operator checklist references bare-metal gate as runtime completion requirement
- [ ] Idempotent scheduler and duplicate-event safety preserved (story 45)

## Blocked by

- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)
- [ci-rt-07-virtiofs-workspace-guest.md](./ci-rt-07-virtiofs-workspace-guest.md) (ci-rt-07)
- [ci-rt-08-vsock-in-guest-execution.md](./ci-rt-08-vsock-in-guest-execution.md) (ci-rt-08)

## User stories covered

- 6 — KVM-verified E2E gate so compose green is not mistaken for production isolation
- 43 — Bare-metal Firecracker E2E in release checklist
- 44 — Process-sandbox compose E2E remains for fast feedback
- 45 — Idempotent behavior under duplicate events
- 46 — Existing parser/scheduler semantics unchanged
- 47 — Public repo log ACL preserved
- 48 — Private repo log ACL preserved

## Notes

- Marks completion of the runtime PRD program when this slice is done.
- Optional compose Firecracker profile (ci-rt-15) is not a substitute for this gate.
