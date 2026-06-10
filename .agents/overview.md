# Project overview — OpenGitBase

This repo may include a **.NET backend**, a **Flutter frontend**, both, or evolve as stacks are added. See [state.md](state.md) for what is enabled in *this* project.

## Top-level layout

```
OpenGitBase/
├── .agentGenCli.json       # manifest — init options, features, command log
├── .agents/                # agent onboarding (this folder)
├── AGENTS.md               # entry point for AI agents
├── applications/           # runnable apps (API and/or Flutter)
├── common/                 # shared .NET library (when backend=dotnet)
├── features/               # backend feature modules (when backend=dotnet)
├── common/{Name}.Cqrs/     # vendored CQRS (when backend=dotnet)
├── common/{Name}.Cqrs.EfCore/
├── tests/                  # .NET test projects (when backend=dotnet)
├── docker-compose.yml      # local stack (when backend=dotnet)
└── OpenGitBase.sln     # .NET solution (when backend=dotnet)
```

## Optional stacks

| Stack | Typical paths | Guide |
|-------|---------------|-------|
| Backend (dotnet) | `applications/OpenGitBase.Api/`, `common/OpenGitBase.Common/`, `features/` | [backend.md](backend.md) if present |
| Frontend (flutter) | `applications/opengitbase/` | [frontend.md](frontend.md) if present |

Do not assume both stacks exist. [state.md](state.md) lists backend/frontend values from init and which guide files were scaffolded.

## Feature organization

- **Backend features** live under `features/{feature-name}/` with Contracts, Feature, and Tests projects (see [backend.md](backend.md)).
- **Frontend features** live under `applications/opengitbase/lib/features/{feature-name}/` (see [frontend.md](frontend.md)).

Use `agentGenCli new …` to add features so registration and routing stay consistent.
