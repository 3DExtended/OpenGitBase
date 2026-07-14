# Compose stack verification — OpenGitBase

Use this when the skill requires running against the local Docker Compose fleet.

## Start or refresh stack

```bash
# From repo root — ensure docker-compose.override.yml exists (copy from example if needed)
docker compose up -d --build
```

Health checks:

```bash
curl -sf http://localhost:8089/health   # API (compose override port)
curl -sf http://localhost:8080/health     # alternate / legacy port — use whichever responds
```

Apply migrations when schema changed:

```bash
agentGenCli project efmigrate
# or project-specific migration command documented in .agents/backend.md
```

Rebuild services touched by the change (API, web, dispatcher) before E2E:

```bash
docker compose up -d --build api-1 api-2   # adjust service names to what changed
```

## Test layers (run what applies)

| Check | Command | When |
|-------|---------|------|
| All .NET tests | `dotnet test` | Always after backend changes |
| Feature-scoped tests | `dotnet test tests/OpenGitBase.Features.{Feature}.Tests` | Faster feedback during TDD |
| Discussions E2E | `./scripts/test-discussions-e2e.sh` | Discussion API / comment / sub-thread work |
| Repo browse E2E | `./scripts/test-repo-browse-e2e.sh` | Repository browsing / git HTTP |
| HA storage E2E | `./scripts/test-ha-storage-e2e.sh` | Storage node / proxy changes |
| Web unit | `cd applications/opengitbase-web && pnpm test` | TS/Vue logic |
| Visual snapshots | `cd applications/opengitbase-web && pnpm test:visual` | Any UI appearance change |

E2E scripts expect the compose stack to be up. They fail fast with a clear message if the API is unreachable.

## Web app in compose vs Playwright dev server

- **Playwright visual tests** use `playwright.config.ts` webServer (`pnpm dev` on port 3000) with MSW — no compose required for gallery snapshots.
- **Compose E2E scripts** hit the real API (`API_URL` / `API_BASE`, default `http://localhost:8089`).

Run both when a work item spans backend API and UI.

## Record in implementation notes

List exact commands and outcomes, e.g.:

```md
- `dotnet test tests/OpenGitBase.Features.Discussion.Tests` — 57 passed
- `./scripts/test-discussions-e2e.sh` — passed
- `pnpm test` — 19 passed
- `pnpm test:visual` — 12 passed (3 viewports × 4 specs)
```
