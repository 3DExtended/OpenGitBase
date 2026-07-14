# Code structure — OpenGitBase

Extend existing patterns; do not duplicate infrastructure. See [engineering-contract](skills/engineering-contract/SKILL.md) for TDD and module depth.

## Repository layout

```
applications/
  OpenGitBase.Api/          # ASP.NET control plane
  OpenGitBase.Cli/          # ogb CLI (ogb)
  OpenGitBase.Dispatcher/   # Git Smart HTTP / SSH proxy
  opengitbase-web/          # Nuxt 4 SPA
  repo-storage-layer/       # Storage node runtime
common/                     # DbContext, CQRS, auth, shared
features/{name}/            # Backend vertical slices
tests/                      # .NET + E2E
docs/prd|adr|issues/        # Spec mirror (forge export)
planning/                   # TDD execution logs
```

## Backend features

Use CLI — do not hand-roll layout:

```bash
agentGenCli new backend-feature MyFeature --withDatabase --withApi --yes
```

Each feature:

- `OpenGitBase.Features.{Feature}.Contracts/` — DTOs, queries
- `OpenGitBase.Features.{Feature}/` — handlers, entities
- `tests/OpenGitBase.Features.{Feature}.Tests/` — handler tests + meta-test

Register assemblies via `FeatureRegistration.cs` marker blocks.

**CQRS:** Handlers extend EfCore bases; controllers map HTTP → queries.

## CLI (`ogb`)

Deep modules — extend, don't fork:

| Module | Role |
|--------|------|
| `CliApp` | Command routing only |
| `*CommandHandlers` | Validate flags → resolve context → API → output |
| `IOgbApiClient` | REST paths, auth, JSON |
| `IOutputWriter` | Human + `--json` |
| `IGitRemoteResolver` / `IGitBranchResolver` | Repo/branch context |
| `RepoContextResolver` | `-R` + URL builders |

New command groups follow `issue` / `mr` patterns.

## Web UI

```
applications/opengitbase-web/
  app/pages/          # Routes
  app/components/     # Vue SFCs
  app/composables/    # Shared logic
  tests/visual/       # Playwright snapshots
  app/pages/__visual__/  # Gallery fixtures
```

MSW for unit/visual tests; compose for API integration E2E.

## Specs and code

- Forge Discussion = canonical spec
- Git mirror = searchable export
- Implementation commits reference forge `#N` in messages when closing slices

## Do not hand-edit

- `FeatureRegistration.cs` assembly list (use agentGenCli)
- Generated OpenAPI client artifacts
- Playwright baselines without `pnpm test:visual:update`

See [backend.md](backend.md) for migrations, testing meta-tests, and local run.
