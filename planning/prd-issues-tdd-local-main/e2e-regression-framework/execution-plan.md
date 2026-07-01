# PRD issues TDD (main) — E2E Regression Framework

Source: [docs/prd/e2e-regression-framework.md](../../../docs/prd/e2e-regression-framework.md)  
Work items: [docs/issues/e2e-regression-framework/README.md](../../../docs/issues/e2e-regression-framework/README.md)

Branch strategy: **main** (all work items committed sequentially on default branch).

**Status: complete** (all 20 work items implemented 2026-07-01).

## Execution order

| Order | ID | Title | Status | Blocked by |
|------:|-----|-------|--------|------------|
| 1 | e2e-01 | Runner skeleton + fast compose + Tier 0 smoke | completed | — |
| 2 | e2e-02 | Operation transcript + auto-logging API client | completed | e2e-01 |
| 3 | e2e-03 | Baseline manager + update-baselines | completed | e2e-02 |
| 4 | e2e-04 | Static HTML report + browser flags | completed | e2e-03 |
| 5 | e2e-05 | Tier orchestrator + skip recording | completed | e2e-04 |
| 6 | e2e-06 | Test isolation + normalization | completed | e2e-05 |
| 7 | e2e-07 | Capturing email sender + E2E mail API | completed | e2e-06 |
| 8 | e2e-08 | Identity seed + auth journey | completed | e2e-07 |
| 9 | e2e-09 | Git facade + HTTPS PAT | completed | e2e-08 |
| 10 | e2e-10 | Playwright invoker + UI tier | completed | e2e-04, e2e-08 |
| 11 | e2e-11 | URL discovery + skeleton generator | completed | e2e-10 |
| 12 | e2e-12 | Security auth matrix | completed | e2e-08 |
| 13 | e2e-13 | Optional fuzz tier | completed | e2e-12 |
| 14 | e2e-14 | Full-HA profile + chaos helpers | completed | e2e-01 |
| 15 | e2e-15 | HA storage chaos scenarios | completed | e2e-09, e2e-14 |
| 16 | e2e-16 | Merge request E2E | completed | e2e-09 |
| 17 | e2e-18 | Discussion E2E | completed | e2e-07, e2e-08 |
| 18 | e2e-19 | Repository browse E2E | completed | e2e-09 |
| 19 | e2e-20 | Runner docs + shell retirement | completed | e2e-09,15,16,18,19 |

## Entry point

```bash
dotnet run --project tests/OpenGitBase.E2E.Runner
```

See [tests/OpenGitBase.E2E/README.md](../../../tests/OpenGitBase.E2E/README.md).
