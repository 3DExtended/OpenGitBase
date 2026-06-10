# agentGenCli commands

Run from the project root (where `.agentGenCli.json` lives). Check [state.md](state.md) for which stacks are enabled before using stack-specific commands.

## Init (already done for this project)

```bash
agentGenCli init project <name> [backend] [frontend]
```

| Argument | Values | Default |
|----------|--------|---------|
| `backend` | `dotnet` (others not yet implemented) | `dotnet` |
| `frontend` | `flutter`, `none` | `flutter` |

### `init email` — requires dotnet backend

```bash
agentGenCli init email [--project NAME] [--yes]
```

Scaffolds SendGrid email support (`common/OpenGitBase.Common.SendGrid/`), patches DI, and adds `SendGrid` appsettings placeholders.

### `init auth` — requires dotnet backend; auto-runs `init email` if needed

```bash
agentGenCli init auth [--project NAME] [--yes]
```

Scaffolds Users feature, JWT + IUserContext, register/sign-in controllers, encrypted email storage, and Flutter auth screens (when a Flutter app exists). Runs `project sync-openapi` when Flutter is present.

Configure after init: `Jwt`, `Encryption`, `SendGrid`, Google/Apple client IDs.

## New features

### `new backend-feature` — requires dotnet backend

```bash
agentGenCli new backend-feature <name> [--withDatabase] [--withApi] [--crud CRUD] [--project NAME] [--yes]
```

| Flag | Effect |
|------|--------|
| `--withDatabase` | Entity + EF configuration; enables `--crud` |
| `--withApi` | Generates API controller + tests; syncs OpenAPI if Flutter app exists |
| `--crud` | Letters `C`, `R`, `U`, `D` (default `CRUD` when `--withDatabase`) |
| `--yes` | Skip confirmation |

Creates `features/{name}/` projects, patches `FeatureRegistration.cs`, optionally runs EF migration.

### `new frontend-feature` — requires flutter frontend

```bash
agentGenCli new frontend-feature <name> [--withApi] [--project NAME] [--yes]
```

| Flag | Effect |
|------|--------|
| `--withApi` | Wire service to Swagger client when matching backend API exists |
| `--yes` | Skip confirmation |

Creates `lib/features/{name}/`, patches `app_router.dart`, runs golden tests.

### `new efmigration` — requires dotnet backend

```bash
agentGenCli new efmigration [--project NAME]
```

Creates a pending EF Core migration from model changes.

## Project maintenance

### `project sync-openapi` — requires dotnet backend **and** flutter frontend

```bash
agentGenCli project sync-openapi [--project NAME]
```

Builds API, exports `swagger/swagger.json`, regenerates Dart client in `lib/generated/swaggen/`.

Run after backend API changes when the Flutter app consumes the API.

### `project efmigrate` — requires dotnet backend

```bash
agentGenCli project efmigrate [--project NAME]
```

Applies pending EF Core migrations to the configured database.

## Typical workflows

**Backend-only API change**

1. `agentGenCli new backend-feature Orders --withDatabase --withApi --yes`
2. `dotnet test`

**Full-stack feature**

1. `agentGenCli new backend-feature Orders --withDatabase --withApi --yes`
2. `agentGenCli new frontend-feature orders --withApi --yes`
3. If API changed later: `agentGenCli project sync-openapi`

**Verify locally**

- Backend: `dotnet test`, `docker compose up -d --build`, `http://localhost:8080/health`
- Frontend: `fvm flutter test` from `applications/opengitbase/`
