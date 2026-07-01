# E2E regression framework — progress log

Branch: `main`

## 2026-07-01 — Full implementation (e2e-01 … e2e-20)

Implemented unified C# E2E regression framework on `main` in one cohesive pass (prior uncommitted WIP consolidated and stabilized).

### Verification

- `OPENGITBASE_E2E_SKIP_COMPOSE=1 dotnet test tests/OpenGitBase.E2E.Tests --filter "Category!=Discovered"` — 19 passed (two consecutive runs)
- `dotnet build tests/OpenGitBase.E2E.Runner` — succeeded
- `tests/OpenGitBase.Common.Tests/SendGrid/CapturingSendGridEmailSenderTests.cs` — included in solution
- Compose API healthy at `http://localhost:8089/health` with `docker-compose.e2e.yml` overlay

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
| e2e-10 | done | Playwright invoker + `@regression` tag on shell spec |
| e2e-11 | done | URL discovery + TestGenerator + unit test |
| e2e-12 | done | Security auth matrix |
| e2e-13 | done | Fuzz tier + `--fuzz` flag |
| e2e-14 | done | Full-HA profile + ClusterChaos |
| e2e-15 | done | HA storage chaos scenario |
| e2e-16 | done | Merge request E2E |
| e2e-18 | done | Discussion E2E (3 scenarios) |
| e2e-19 | done | Repository browse E2E |
| e2e-20 | done | README docs + retired shell e2e scripts |

### Key fixes during stabilization

- Per-test-method baseline paths via `BeginScenario()`
- Baseline normalizer: durations, PATs, traceIds, temp dirs, git errors
- FuzzRunner maps `ExpectedOutcome` to HTTP status codes
- `OPENGITBASE_E2E_SKIP_COMPOSE` no longer skips compose-backed tests when stack is up
