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
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests` (prior slices); operator script verified structurally

### ci-rt-06 — Real Firecracker launcher + VM lifecycle
- **Commit:** (pending)
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter FullyQualifiedName~Firecracker` (5 passed)

## Remaining (9 items)

ci-rt-07, ci-rt-08, ci-rt-09, ci-rt-10, ci-rt-11, ci-rt-12, ci-rt-13, ci-rt-15, ci-rt-16
