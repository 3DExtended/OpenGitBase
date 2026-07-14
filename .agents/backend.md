# Backend guide — OpenGitBase

.NET backend using CQRS, EF Core, and feature modules. This guide applies because this project was initialized with `backend=dotnet`.

## Layout

```
applications/OpenGitBase.Api/     # ASP.NET Web API entry point
common/OpenGitBase.Common/        # DbContext, shared models, health check
features/{feature}/                   # one folder per backend feature
  OpenGitBase.Features.{Feature}.Contracts/
  OpenGitBase.Features.{Feature}/
tests/OpenGitBase.Features.{Feature}.Tests/
common/OpenGitBase.Cqrs/          # vendored CQRS core (queries, handlers, Option)
common/OpenGitBase.Cqrs.EfCore/   # EF Core base query handlers (Mapster)
tests/OpenGitBase.Common.Tests/
tests/OpenGitBase.Api.Tests/
```

## CQRS pattern

Queries and handlers live in feature projects. The API layer maps HTTP to queries via generated or hand-written controllers. Shared CQRS infrastructure is vendored in `common/OpenGitBase.Cqrs` and `common/OpenGitBase.Cqrs.EfCore`; database and DI wiring live in `OpenGitBase.Common`.

Domain models in feature Contracts projects extend `ModelBase<{Name}Id, Guid>` with a matching `{Name}Id : Identifier<Guid, {Name}Id>` record. For database-backed CRUD features, subclass the EfCore base handlers (`CreateQueryHandlerBase`, `SingleModelQueryHandlerBase`, etc.) and register Mapster maps via `{Feature}MapsterConfig` (`IRegister`).

## Adding a feature

Use the CLI — do not copy-paste project structure:

```bash
agentGenCli new backend-feature MyFeature --withDatabase --withApi --yes
```

This creates Contracts / Feature / Tests projects, wires solution references, and patches:

```csharp
// applications/OpenGitBase.Api/FeatureRegistration.cs
// agentGenCli:feature-assemblies
```

Each new feature adds its assembly to that list. Avoid manual edits unless you understand the registration model.

## Database and migrations

- DbContext: `common/OpenGitBase.Common/Data/OpenGitBaseDbContext.cs`
- `--withDatabase` scaffolds entity + EF configuration under the feature's `database/` folder
- Initial migration is created at project init; new features with database get `Add{Feature}` migrations
- Apply migrations: `agentGenCli project efmigrate`

## API and Swagger

- Controllers generated with `--withApi` from CRUD query/handler templates
- Swagger filters in `applications/OpenGitBase.Api/Swagger/`
- OpenAPI export used by Flutter when frontend is enabled: `agentGenCli project sync-openapi`

## Testing

Every query handler and API controller must have a matching test class. Meta-tests enforce this at `dotnet test` by reflecting over each production assembly and failing when `{Name}Tests` is missing.

| Production assembly | Test project | Meta-test | Unit/integration tests |
|---------------------|--------------|-----------|------------------------|
| `OpenGitBase.Common` | `OpenGitBase.Common.Tests` | `QueryHandlers/QueryHandlerCoverageTests.cs` | `QueryHandlers/.../{Name}QueryHandlerTests.cs` |
| `OpenGitBase.Features.{Feature}` | `OpenGitBase.Features.{Feature}.Tests` | `QueryHandlers/QueryHandlerCoverageTests.cs` | `QueryHandlers/{Name}QueryHandlerTests.cs` |
| `OpenGitBase.Api` | `OpenGitBase.Api.Tests` | `Controllers/ControllerCoverageTests.cs` | `Controllers/{Name}ControllerTests.cs` |
| `OpenGitBase.Cli` | `OpenGitBase.Cli.Tests` | `CommandHandlerCoverageTests` | `*CommandTests`, `*CommandExtendedTests` |

Opt out with `[ExcludeFromCoverageTests]` from `OpenGitBase.Common` when a type genuinely should not require a test class.

`new backend-feature` scaffolds handler test stubs for every handler it creates. API controller tests use the `E2ETest` environment (no database/migrations).

```bash
dotnet test
```

## Local run

```bash
docker compose up -d --build
curl http://localhost:8080/health
dotnet test
```

## Do not hand-edit

- `FeatureRegistration.cs` assembly list (use `new backend-feature` or follow marker pattern)
- Generated controller test scaffolding tied to CRUD letters — regenerate via CLI flags when possible
