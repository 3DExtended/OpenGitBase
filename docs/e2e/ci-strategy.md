# E2E CI strategy

Human review sign-off for smoke vs regression job matrix. Commands use runner tag filters from pop-05.

## Review sign-off

| Reviewer | Date | Decision |
|----------|------|----------|
| _Pending team review_ | — | Draft for local workflow; no mandatory GHA merge gate in v1 |

## Job matrix

| Job | When | Command | Target runtime |
|-----|------|---------|----------------|
| PR gate | Every PR | `dotnet test` (solution) + `dotnet test tests/OpenGitBase.E2E.Tests --filter Category=E2EUnit` | &lt;5 min |
| Smoke E2E | PR optional / pre-merge local | `dotnet run --project tests/OpenGitBase.E2E.Runner -- --skip-compose --tag Smoke --no-open-report` | ~15–25 min |
| Regression E2E | Nightly | `dotnet run --project tests/OpenGitBase.E2E.Runner -- --profile fast --tag Regression --no-open-report` | ~45–90 min |
| Full-HA | Weekly / pre-release | `dotnet run --project tests/OpenGitBase.E2E.Runner -- --profile full-ha --tag FullHa --no-open-report` | ~2–4 h |
| Playwright UI | Nightly | `dotnet run --project tests/OpenGitBase.E2E.Runner -- --tier 8 --no-open-report` | ~10–20 min |

## Machine requirements

- Docker Desktop or Linux Docker with Compose v2
- 16 GB RAM recommended for full stack; 8 GB minimum for fast profile
- `docker-compose.override.yml` from example + `bootstrap-fleet.sh` for storage fleet
- Disk: ~20 GB for images and git testdata pushes
- Node 20+ and `pnpm` for Playwright tier 8

## Runtime budgets (PRD targets)

- **Smoke**: &lt;30 min wall clock with warm compose
- **Regression (fast)**: &lt;90 min
- **Full-HA**: &lt;4 h including chaos tier
- **Playwright tier 8**: &lt;20 min

## Explicit deferrals (v1)

- No required GitHub Actions workflow in this repository until release engineering allocates runners with Docker-in-Docker
- Rate-limit (`@Slow`) scenarios excluded from PR smoke; nightly regression only
- SSH git profile (`--profile ssh`) manual until dedicated compose overlay lands
- Primary storage failover chaos deferred to full-HA weekly job

## Filter reference

```bash
# Daily developer smoke
dotnet run --project tests/OpenGitBase.E2E.Runner -- --skip-compose --tag Smoke --no-open-report

# Single feature while authoring
dotnet run --project tests/OpenGitBase.E2E.Runner -- --skip-compose --feature Discussion --tag Regression --no-open-report

# Promotion indexer (manual)
dotnet run --project tests/OpenGitBase.E2E.Runner -- promote-index
```

## Out of scope for v1

- Automatic baseline update in CI (local `--update-baselines` only)
- Parallel shard orchestration across multiple compose stacks
- Publishing HTML reports to remote storage (local `.e2e-reports/` only)
