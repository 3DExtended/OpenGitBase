# CI/CD runtime completion — execution plan

**PRD:** `docs/prd/ci-cd-runtime-completion.md`  
**Work items:** `planning/ci-cd-runtime-completion/items/`  
**Branch strategy:** **main** (all work items committed sequentially on default branch).

## Topological order

1. ci-rt-01 — Node Identity + agent API authentication
2. ci-rt-03 — CI variables, org context, cancel write ACL
3. ci-rt-04 — PRD recipe keys + install fail-fast
4. ci-rt-05 — Base image build script + vsock guest agent in rootfs
5. ci-rt-14 — Org egress approval on compute settings page
6. ci-rt-02 — Workspace archive API + Job Identity fetch
7. ci-rt-06 — Real Firecracker launcher + VM lifecycle
8. ci-rt-12 — SSE log streaming + incremental agent posts
9. ci-rt-07 — Virtio-fs workspace in guest
10. ci-rt-09 — Firecracker resource limits from job spec
11. ci-rt-10 — Host egress nftables + compose allowlist seed
12. ci-rt-15 — Compose Firecracker profile
13. ci-rt-08 — Vsock in-guest install + script execution
14. ci-rt-13 — Cancel propagation (Kafka + poll + VM teardown)
15. ci-rt-11 — Layer promotion jobs + real overlay deltas
16. ci-rt-16 — Bare-metal Firecracker E2E gate

## Status

| ID | Title | Status |
|----|-------|--------|
| ci-rt-01 | Node Identity + agent API authentication | completed (`6fdd512`) |
| ci-rt-02 | Workspace archive API + Job Identity fetch | pending |
| ci-rt-03 | CI variables, org context, cancel write ACL | completed (`c366e8e`) |
| ci-rt-04 | PRD recipe keys + install fail-fast | completed (`f39a354`) |
| ci-rt-05 | Base image build script + vsock guest agent in rootfs | pending |
| ci-rt-06 | Real Firecracker launcher + VM lifecycle | pending |
| ci-rt-07 | Virtio-fs workspace in guest | pending |
| ci-rt-08 | Vsock in-guest install + script execution | pending |
| ci-rt-09 | Firecracker resource limits from job spec | pending |
| ci-rt-10 | Host egress nftables + compose allowlist seed | pending |
| ci-rt-11 | Layer promotion jobs + real overlay deltas | pending |
| ci-rt-12 | SSE log streaming + incremental agent posts | pending |
| ci-rt-13 | Cancel propagation (Kafka + poll + VM teardown) | pending |
| ci-rt-14 | Org egress approval on compute settings page | pending |
| ci-rt-15 | Compose Firecracker profile | pending |
| ci-rt-16 | Bare-metal Firecracker E2E gate | pending |
