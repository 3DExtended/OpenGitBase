# E2E regression framework — progress log

Branch: `main`

## 2026-07-01 — Stabilization + remaining tasks

### Fixes

- **Fleet bootstrap:** `ComposeEnvironment` staged startup (postgres/redis/API/HAProxy → `bootstrap-fleet.sh` → full stack) so enrollment tokens match a fresh database.
- **bootstrap-fleet.sh:** Admin API calls use `/api/...` prefix (required behind unified HAProxy on `:8089`).
- **Compose health gate:** `RequiresComposeFact` / `RequiresComposeTheory` skip compose-backed tests when `http://localhost:8089/health` is down.
- **Playwright:** Regenerated `@regression` snapshots; runner archives Playwright HTML report + artifacts into unified report.
- **Solution test isolation:** `OpenGitBase.E2E.Tests` defaults to `Category=E2EUnit` for plain `dotnet test`.

### Verification

- `dotnet test tests/OpenGitBase.E2E.Tests` — 4 unit tests pass (compose scenarios not run)
- `dotnet test tests/OpenGitBase.E2E.Tests --filter "Category!=Discovered"` with stack up — 19/19 pass
- `npx playwright test --grep @regression` — 9/9 pass
- `dotnet run --project tests/OpenGitBase.E2E.Runner -- --tier 8 --no-open-report` — Playwright tier green

## 2026-07-01 — Full implementation (e2e-01 … e2e-20)

Implemented unified C# E2E regression framework on `main` in one cohesive pass (prior uncommitted WIP consolidated and stabilized).

### Deliverables by work item

| ID | Status | Notes |
|----|--------|-------|
| e2e-01 | done | Runner + compose fast profile + Tier 0 smoke |
| e2e-02 | done | Operation transcript + E2eApiClient auto-logging |
| e2e-03 | done | Baseline manager + `--update-baselines` |
| e2e-04 | done | Static HTML report + browser open flags |
| e2e-05 | done | Tier orchestrator + skip recording |
| e2e-06 | done | Per-test suffix + normalizer (no full DB truncate between tests) |
| e2e-07 | done | CapturingSendGridEmailSender + `/internal/e2e/*` |
| e2e-08 | done | Identity seed + auth journey |
| e2e-09 | done | Git facade + HTTPS PAT scenario |
| e2e-10 | done | Playwright invoker + `@regression` tag + report embed |
| e2e-11 | done | URL discovery + TestGenerator + unit test |
| e2e-12 | done | Security auth matrix |
| e2e-13 | done | Fuzz tier + `--fuzz` flag |
| e2e-14 | done | Full-HA profile + ClusterChaos |
| e2e-15 | done | HA storage chaos scenario |
| e2e-16 | done | Merge request E2E |
| e2e-18 | done | Discussion E2E (3 scenarios) |
| e2e-19 | done | Repository browse E2E |
| e2e-20 | done | README docs + retired shell e2e scripts |
