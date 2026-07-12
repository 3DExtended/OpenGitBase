# CI/CD PRD completion — progress log

## ci-prd-01 — Platform compute bootstrap + admin fleet UI

- **Status:** completed
- **Commit:** `cbe282c`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter AdminCompute`, `pnpm test:visual:update -- tests/visual/admin-compute.spec.ts`

## ci-prd-02 — Org compute settings UI + enrollment API hardening

- **Status:** completed
- **Commit:** `f268e66`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter OrganizationComputeControllerTests`, `pnpm test:visual:update -- tests/visual/org-compute.spec.ts`

## ci-prd-03 — Org compute enroll → job routing integration test

- **Status:** completed
- **Commit:** `c0e525e`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter OrgComputeIntegration`

## ci-prd-04 — Base image catalog build + Layer Store artifacts

- **Status:** completed
- **Commit:** `62dc640`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests --filter ResolveBaseImageBySlug`

## ci-prd-05 — Firecracker MicroVM executor + operator requirements

- **Status:** completed
- **Commit:** `38508d6`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter FirecrackerSandbox`

## ci-prd-06 — OverlayFS stack assembly in compute agent

- **Status:** completed
- **Commit:** `b912e2d`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter OverlayFsStackAssembler`

## ci-prd-07 — Firecracker `ogb-hosted` tracer

- **Status:** completed
- **Commit:** `a47dfb5`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter OverlayFsStackAssembler|FirecrackerSandbox`

## ci-prd-08 — Layer promotion runtime + promoted layer mount

- **Status:** completed
- **Commit:** `3737fed`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests --filter DependencyLayerPromotionRuntime`

## ci-prd-09 — Host egress enforcement in compute agent

- **Status:** completed
- **Commit:** `e1cec43`
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter HostEgressEnforcer`

## ci-prd-10 — Job Identity security contract tests

- **Status:** completed
- **Commit:** `7e99382`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests --filter JobIdentitySecurity`

## ci-prd-11 — Admin CI console: base images + promotion dashboard

- **Status:** completed
- **Commit:** `7d42fe8`
- **Tests:** `pnpm test`, `pnpm test:visual:update -- tests/visual/admin-ci.spec.ts`

## ci-prd-12 — Admin + org domain allowance review UI

- **Status:** completed
- **Commit:** `c69d6d9`
- **Tests:** `dotnet test tests/OpenGitBase.Features.Pipeline.Tests --filter SubmitDomainAllowance|ReviewDomainAllowance`, `pnpm test`

## ci-prd-13 — Pipeline log visibility + live streaming UI

- **Status:** completed
- **Commit:** `c58b7a9`
- **Tests:** `pnpm test` (run detail section grouping + 4s poll while jobs run)

## ci-prd-14 — Compose bootstrap E2E gate

- **Status:** completed
- **Commit:** `3d7a5e8`
- **Tests:** `scripts/test-pipelines-e2e.sh` (after `scripts/bootstrap-fleet.sh`)

## ci-prd-15 — Community-hosted hybrid tracer

- **Status:** completed
- **Commit:** `3999710` (marker; test in `c0e525e`)
- **Tests:** `dotnet test tests/OpenGitBase.Api.Tests --filter CommunityHostedIntegration`
