# OpenGitBase E2E Regression Framework

Unified local regression entry point for infrastructure smoke, auth, git HTTPS, feature scenarios, HA chaos, Playwright UI checks, and optional fuzz tiers.

## Quick start

```bash
# Full suite (starts Docker Compose, bootstraps fleet, runs tiers, writes report)
dotnet run --project tests/OpenGitBase.E2E.Runner

# Stack already running — skip compose, update committed baselines
dotnet run --project tests/OpenGitBase.E2E.Runner -- --skip-compose --update-baselines --no-open-report

# Playwright UI tier only (no compose)
dotnet run --project tests/OpenGitBase.E2E.Runner -- --tier 8 --no-open-report
```

### Unit/meta tests (no compose)

```bash
dotnet test tests/OpenGitBase.E2E.Tests
```

Runs only `Category=E2EUnit` tests (transcript, normalizer, report, URL discovery). Compose-backed scenarios are skipped automatically when the stack is down (`RequiresCompose*` attributes).

### Solution-wide `dotnet test`

`OpenGitBase.E2E.Tests` defaults to unit/meta only so `dotnet test OpenGitBase.sln` does not require Docker. Full regression:

```bash
dotnet run --project tests/OpenGitBase.E2E.Runner
```

## CLI flags

| Flag | Effect |
|------|--------|
| `--profile fast\|full-ha` | Compose profile (default: fast). Full HA adds `docker-compose.e2e-full-ha.yml`. |
| `--skip-compose` | Assume stack is already up at `http://localhost:8089`. |
| `--update-baselines` | Write golden files under `tests/OpenGitBase.E2E.Tests/Baselines/`. |
| `--open-report` | Open static HTML report after run. |
| `--no-open-report` | Never open browser. |
| `--fuzz` | Run optional fuzz tier. |
| `--tier <n>` | Run a single tier only (e.g. `8` = Playwright UI). |
| `--filter <expr>` | Pass xUnit filter to tier test runs. |

## Compose overlays

- `docker-compose.e2e.yml` — sets `E2E__CaptureEmail=true` on API services for in-memory email capture and internal `/internal/e2e/*` endpoints.
- `docker-compose.e2e-full-ha.yml` — full HA profile marker (uses main multi-node topology).
- `docker-compose.override.yml` — **required** for storage fleet (auto-included when present). The runner calls `./scripts/bootstrap-fleet.sh` after API health is up to refresh enrollment tokens in this file. On first setup, copy from `docker-compose.override.example.yml`.

## Reports

Local only: `.e2e-reports/<timestamp>/index.html` with `latest/` copy. Playwright HTML report and screenshot artifacts are copied under `playwright/` in each run directory.

## Replaces shell scripts

This runner supersedes the retired `scripts/*e2e*.sh` integration scripts. Run:

```bash
dotnet run --project tests/OpenGitBase.E2E.Runner
```

## Scenario catalog

All compose-backed scenarios are indexed in [docs/e2e/scenario-catalog.md](../../docs/e2e/scenario-catalog.md). Add or update a catalog row in the **same PR** as new tests.

## Authoring checklist

When adding a scenario:

1. Pick a catalog ID (`E2E-Fxx-nnn`) and set status `in-progress` → `done`.
2. Add `[Trait("Category", "<Feature>")]` and `[E2eTier(n)]` matching the tier registry.
3. Tag `[Trait("Smoke")]` and/or `[Trait("Regression")]` (and `[Trait("FullHa")]` when required).
4. Call `BeginScenario()` at the start of the test method (per-method baseline path).
5. Add `Transcript.Describe("…")` intent sentences at logical steps.
6. Capture baselines via `Baselines.CaptureApiAsync` / git / side-channel helpers.
7. Run with stack up: `dotnet run --project tests/OpenGitBase.E2E.Runner -- --skip-compose --filter "FullyQualifiedName~YourTest" --update-baselines --no-open-report`.
8. Commit baseline bundles under `tests/OpenGitBase.E2E.Tests/Baselines/`.
9. Map to at least one PRD user story or parity issue in the catalog row.

## Quality gates

**Include** scenarios that:

- Prove observable behavior across HTTP, git, storage, email, or HAProxy.
- Have committed baselines and a human-readable transcript.
- Fill a catalog row traced to PRD or shell-parity acceptance criteria.

**Reject** scenarios that:

- Duplicate handler-only integration tests with no cross-service proof.
- Assert only status codes without baseline capture.
- Depend on test execution order or shared DB state without run-suffix isolation.
- Lack a catalog row or PRD/parity trace.
