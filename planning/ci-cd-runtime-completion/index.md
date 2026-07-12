# CI/CD runtime completion — work items

Vertical slices to implement [PRD: CI/CD Runtime Completion](../../docs/prd/ci-cd-runtime-completion.md) on top of the existing control-plane tracer (ci-01…ci-20, ci-prd-01…15).

**Done when:** ci-rt-16 bare-metal Firecracker E2E gate passes; compose process-sandbox E2E remains green.

## Status

| ID | Title | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| ci-rt-01 | [Node Identity + agent API authentication](./items/ci-rt-01-node-identity-agent-auth.md) | AFK | ready | — |
| ci-rt-02 | [Workspace archive API + Job Identity fetch](./items/ci-rt-02-workspace-archive-job-identity.md) | AFK | ready | ci-rt-01 |
| ci-rt-03 | [CI variables, org context, cancel write ACL](./items/ci-rt-03-ci-variables-cancel-acl.md) | AFK | ready | — |
| ci-rt-04 | [PRD recipe keys + install fail-fast](./items/ci-rt-04-recipe-keys-install-failfast.md) | AFK | ready | — |
| ci-rt-05 | [Base image build script + vsock guest agent in rootfs](./items/ci-rt-05-base-image-guest-agent.md) | AFK | ready | — |
| ci-rt-06 | [Real Firecracker launcher + VM lifecycle](./items/ci-rt-06-firecracker-launcher.md) | AFK | ready | ci-rt-05 |
| ci-rt-07 | [Virtio-fs workspace in guest](./items/ci-rt-07-virtiofs-workspace-guest.md) | AFK | ready | ci-rt-02, ci-rt-06 |
| ci-rt-08 | [Vsock in-guest install + script execution](./items/ci-rt-08-vsock-in-guest-execution.md) | AFK | ready | ci-rt-06, ci-rt-07 |
| ci-rt-09 | [Firecracker resource limits from job spec](./items/ci-rt-09-firecracker-resource-limits.md) | AFK | ready | ci-rt-06 |
| ci-rt-10 | [Host egress nftables + compose allowlist seed](./items/ci-rt-10-host-egress-nftables.md) | AFK | ready | ci-rt-06 |
| ci-rt-11 | [Layer promotion jobs + real overlay deltas](./items/ci-rt-11-layer-promotion-jobs.md) | AFK | ready | ci-rt-04, ci-rt-06, ci-rt-08 |
| ci-rt-12 | [SSE log streaming + incremental agent posts](./items/ci-rt-12-sse-log-streaming.md) | AFK | ready | ci-rt-01 |
| ci-rt-13 | [Cancel propagation (Kafka + poll + VM teardown)](./items/ci-rt-13-cancel-propagation.md) | AFK | ready | ci-rt-03, ci-rt-06 |
| ci-rt-14 | [Org egress approval on compute settings page](./items/ci-rt-14-org-egress-ui.md) | AFK | ready | — |
| ci-rt-15 | [Compose Firecracker profile](./items/ci-rt-15-compose-firecracker-profile.md) | AFK | ready | ci-rt-06 |
| ci-rt-16 | [Bare-metal Firecracker E2E gate](./items/ci-rt-16-bare-metal-firecracker-e2e.md) | AFK | ready | ci-rt-06, ci-rt-07, ci-rt-08 |

## Dependency graph

```
ci-rt-01 ─→ ci-rt-02 ─┬→ ci-rt-07 ─→ ci-rt-08 ─┐
ci-rt-05 ─→ ci-rt-06 ─┤                         ├→ ci-rt-16
                      ├→ ci-rt-09               │
                      ├→ ci-rt-10               │
                      └→ ci-rt-13 ← ci-rt-03    │
ci-rt-04 ────────────────→ ci-rt-11 ← ci-rt-08 ─┘
ci-rt-01 ─→ ci-rt-12
ci-rt-06 ─→ ci-rt-15
ci-rt-14 (independent)
ci-rt-03, ci-rt-04 (parallel starts)
```

## Topological order

1. ci-rt-01, ci-rt-03, ci-rt-04, ci-rt-05, ci-rt-14
2. ci-rt-02, ci-rt-06, ci-rt-12
3. ci-rt-07, ci-rt-09, ci-rt-10, ci-rt-15
4. ci-rt-08, ci-rt-13
5. ci-rt-11
6. ci-rt-16

## Source

- [docs/prd/ci-cd-runtime-completion.md](../../docs/prd/ci-cd-runtime-completion.md)
- Parent: [docs/prd/ci-cd-pipelines.md](../../docs/prd/ci-cd-pipelines.md)
