# CI/CD runtime completion — progress log

## 2026-07-12

### ci-rt-01 — Node Identity + agent API authentication
- **Commit:** `6fdd512`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter FullyQualifiedName~NodeIdentity|...` (6 passed)

### ci-rt-03 — CI variables, org context, cancel write ACL
- **Commit:** `c366e8e`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests` (53 passed)

### ci-rt-05 — Base image build script + vsock guest agent
- **Commit:** `ae8e34b`

### ci-rt-06 — Real Firecracker launcher + VM lifecycle
- **Commit:** `69ec745`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter FullyQualifiedName~Firecracker` (7 passed)

### ci-rt-12 — SSE log streaming + incremental agent posts
- **Commit:** `bfec83a`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests --filter AppendPipelineJobLogs`; `pnpm test:visual -- tests/visual/pipelines.spec.ts`

### ci-rt-07 — Virtio-fs workspace in guest
- **Commit:** `0c51c95`

### ci-rt-09 — Firecracker resource limits from job spec
- **Commit:** `6b7452f`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter FirecrackerResourceLimits`

### ci-rt-10 — Host egress nftables + compose allowlist seed
- **Commit:** `9fd3074`

### ci-rt-15 — Compose Firecracker profile
- **Commit:** `2529292`

### ci-rt-08 — Vsock in-guest install + script execution
- **Commit:** `3152370`

### ci-rt-13 — Cancel propagation (Kafka + poll + VM teardown)
- **Commit:** `fa8b106`

### ci-rt-11 — Layer promotion jobs + real overlay deltas
- **Commit:** `8ea91ee`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests --filter DependencyLayerPromotion`

### ci-rt-16 — Bare-metal Firecracker E2E gate
- **Commit:** `4fa1236`
- **Tests:** `scripts/test-firecracker-bare-metal-e2e.sh` (requires KVM host)

### Worker integration
- **Commit:** `c3e06b8`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter Firecracker`; `dotnet test tests/OpenGitBase.Features.Pipeline.Tests`
