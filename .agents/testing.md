# Testing guide — OpenGitBase

See global skill **`cli-goldens`** and **`visual-snapshots`** for CLI and UI patterns. This file is project-specific.

## Test layers

| Layer | Command | When |
|-------|---------|------|
| All .NET | `dotnet test` | Always after backend/CLI changes |
| Feature scoped | `dotnet test tests/OpenGitBase.Features.{Feature}.Tests` | Faster TDD |
| CLI unit | `dotnet test tests/OpenGitBase.Cli.Tests` | `ogb` changes |
| CLI integration | `dotnet test tests/OpenGitBase.Cli.Integration.Tests` | CLI + in-process API |
| Compose E2E | `dotnet test tests/OpenGitBase.E2E.Tests --filter "RequiresCompose"` | Real stack |
| Domain E2E scripts | `./scripts/test-*-e2e.sh` | See script name for domain |
| Web unit | `cd applications/opengitbase-web && pnpm test` | TS/Vue logic |
| Visual | `cd applications/opengitbase-web && pnpm test:visual` | Any UI appearance change |

## Compose

```bash
docker compose up -d --build
curl -sf http://localhost:8089/health
```

See `.agents/` skill references and `compose-verification.md` in the global `prd-issues-tdd-local-main` skill for migration and rebuild notes.

## Meta-tests (CI gates)

Enforced at `dotnet test`:

| Assembly | Test project | Meta-test |
|----------|--------------|-----------|
| Query handlers | `OpenGitBase.Features.*.Tests` | `QueryHandlerCoverageTests` |
| `OpenGitBase.Common` handlers | `OpenGitBase.Common.Tests` | `QueryHandlerCoverageTests` |
| API controllers | `OpenGitBase.Api.Tests` | `ControllerCoverageTests` |
| CLI `*CommandHandlers` | `OpenGitBase.Cli.Tests` | `CommandHandlerCoverageTests` |

Opt out: `[ExcludeFromCoverageTests]` on types that genuinely need no test class.

**Planned:** docs mirror freshness after `ogb docs pull` ships.

## Change-type checklist

| You changed | You must |
|-------------|----------|
| Query handler | `{Name}QueryHandlerTests` |
| Controller | `{Name}ControllerTests` |
| CLI handler method | Coverage via `*CommandTests` / `*CommandExtendedTests` |
| Vue component/page | Gallery fixture + Playwright snapshot |
| CLI `--json` contract | JSON golden in `Goldens/` (see `cli-goldens` skill) |
| API + compose path | Relevant E2E script or tier test |

## TDD

Tracer-bullet: one test → minimal code → green. With PRD slice: `/tdd` inside `/prd-issues-tdd-local-main`. Regression test before bug fixes.

## Scripts

| Script | Purpose |
|--------|---------|
| `scripts/test-ogb-cli-e2e.sh` | Issue CLI smoke |
| `scripts/test-ogb-cli-mr-e2e.sh` | MR CLI smoke |
| `scripts/test-discussions-e2e.sh` | Discussions API |
| `scripts/test-repo-browse-e2e.sh` | Repo browse / git HTTP |
