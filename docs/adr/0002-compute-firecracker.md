# ADR 0002: Compute Firecracker execution

## Status

Accepted

## Context

CI/CD jobs require per-job MicroVM isolation. Local compose development often lacks `/dev/kvm` or Firecracker binaries.

## Decision

- `ISandboxExecutor` remains the agent boundary.
- `FirecrackerSandboxExecutor` boots one MicroVM per job when KVM and the Firecracker binary are available.
- `ProcessSandboxExecutor` is the explicit dev fallback when `ComputeAgent__PreferProcessSandbox=true` or KVM is unavailable.
- Compose agents run with `PreferProcessSandbox: "true"` by default; bare-metal platform nodes disable it and mount `/dev/kvm`.

## Operator requirements

| Requirement | Platform node | Compose dev |
|-------------|---------------|-------------|
| `/dev/kvm` | Required | Optional |
| Firecracker binary | Required | Optional |
| Privileged container | Often required | Not required with process fallback |
| `PreferProcessSandbox` | `false` | `true` (default) |

## Guest execution

- Default Unix user: `ogb`
- Optional `user: root` in job spec runs script as root inside guest
- VM destroyed on completion, failure, cancel, and timeout

## Consequences

- Production `ogb-hosted` uses Firecracker when KVM is present.
- Compose and macOS dev rely on process sandbox without blocking local iteration.
