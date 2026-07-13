# Progress log — `ogb` CLI

Sequential TDD execution on `main`.

## Run started

- **Date:** 2026-07-12
- **PRD:** `docs/prd/ogb-cli.md`
- **Items:** 15 (cli-01 … cli-15)

---

## 2026-07-12 — cli-01 complete

- **Commit:** `82a7353` — feat(cli): scaffold ogb CLI project bootstrap
- **Tests:** `dotnet test tests/OpenGitBase.Cli.Tests` — 3 passed

## 2026-07-12 — cli-02 complete

- **Commit:** `6abe789` — feat(cli): add host resolver and config store
- **Tests:** `dotnet test tests/OpenGitBase.Cli.Tests` — 13 passed

## 2026-07-12 — cli-03 complete

- **Commit:** `c2e86bc` — feat(web): add Nuxt /cli/auth loopback login page
- **Tests:** `pnpm test app/utils/cliAuthRedirect.test.ts` — 4 passed; `playwright test tests/visual/cli-auth.spec.ts --update-snapshots` — 3 passed

## 2026-07-12 — cli-04 … cli-15 complete

- **Commit:** `4d81732` — feat(cli): auth loopback, issue commands, and JSON output
- **Tests:** `dotnet test tests/OpenGitBase.Cli.Tests` — 34 passed
- **Note:** Items cli-04 through cli-15 landed in one cohesive commit (shared command wiring in `CliApp.cs`).

---

## Final verification

| Check | Result |
|-------|--------|
| `dotnet test tests/OpenGitBase.Cli.Tests` | 34 passed |
| `pnpm test app/utils/cliAuthRedirect.test.ts` | 4 passed |
| `playwright test tests/visual/cli-auth.spec.ts` | 3 passed (snapshots committed) |
| Compose E2E | Not run (no API/schema changes; CLI consumes existing Discussion endpoints) |

---

## 2026-07-12 — maximum test coverage

Expanded CLI test suite from 34 unit tests to full pyramid coverage.

### Unit tests (`tests/OpenGitBase.Cli.Tests`) — 61 passed

| Area | New/expanded files |
|------|---------------------|
| Issue commands | `IssueCommandExtendedTests.cs` |
| Auth / loopback | `LoopbackAuthServerIntegrationTests.cs`, `AuthCommandTests.cs` |
| Credentials | `FileCredentialStoreTests.cs`, `CredentialStoreFactoryTests.cs` |
| Git remote | `GitRemoteResolverEdgeCaseTests.cs` |
| Body / errors | `BodyContentResolverTests.cs`, `CliErrorHandlerTests.cs` |
| API JSON | `FlexibleGuidJsonConverterTests.cs` (identifier `{ value }` wrapper) |
| Shared fakes | `TestSupport/CliTestSupport.cs`, updated `StubHttpMessageHandler.cs` |

### Integration tests (`tests/OpenGitBase.Cli.Integration.Tests`) — 2 passed

In-process API via `WebApplicationFactory` + real `CliApp.RunAsync`:

- `Issue_lifecycle_against_in_process_api` — create, list, comment, status, close
- `Auth_login_stores_token_from_loopback` — fake loopback + credential store

Note: in-process API routes omit `/api` prefix; integration overrides use `OgbApiClient(..., host, host)`.

### Compose E2E (`tests/OpenGitBase.E2E.Tests/Cli/CliIssueE2eTests.cs`)

Tier-4 compose scenario: create → list → close against `localhost:8089/api`. Requires healthy storage nodes (503 if compose capacity exhausted).

Run: `dotnet test tests/OpenGitBase.E2E.Tests -p:VSTestTestCaseFilter="FullyQualifiedName~CliIssueE2eTests"`

### Shell smoke script

`scripts/test-ogb-cli-e2e.sh` — full lifecycle via `dotnet run` + curl against compose (macOS keychain or file fallback).

### Production fix

`FlexibleGuidJsonConverter` — deserializes API `Identifier<T>` JSON (`{ "value": "..." }`) into CLI `Guid` fields.

| Check | Result |
|-------|--------|
| `dotnet test tests/OpenGitBase.Cli.Tests` | **61 passed** |
| `dotnet test tests/OpenGitBase.Cli.Integration.Tests` | **2 passed** |
| `dotnet test tests/OpenGitBase.E2E.Tests -p:VSTestTestCaseFilter=…CliIssue…` | Compiles; needs healthy compose storage |
| `scripts/test-ogb-cli-e2e.sh` | Needs healthy compose storage |
