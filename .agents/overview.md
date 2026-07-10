# Project overview — OpenGitBase

Self-hosted Git forge with a .NET control plane, distributed storage fleet, and Nuxt web UI. See [state.md](state.md) for enabled features and [docs/PROJECT-STATE.md](../docs/PROJECT-STATE.md) for architecture and component interactions.

## Top-level layout

```
OpenGitBase/
├── .agentGenCli.json           # scaffold manifest (original init only)
├── .agents/                    # agent onboarding (this folder)
├── AGENTS.md                   # entry point for AI agents
├── applications/
│   ├── OpenGitBase.Api/        # ASP.NET Core control plane
│   ├── OpenGitBase.Dispatcher/ # git Smart HTTP / SSH proxy
│   ├── opengitbase-web/        # Nuxt 4 web UI
│   └── repo-storage-layer/     # storage node runtime (Python + nginx + sshd)
├── common/                     # shared .NET (CQRS, EF, auth, encryption)
├── features/                   # backend feature modules (vertical slices)
├── tests/                      # .NET + E2E test projects
├── docs/
│   ├── PROJECT-STATE.md        # current implementation baseline
│   ├── prd/                    # product / architecture specs
│   └── issues/                 # implementation slices
├── docker-compose.yml          # local HA stack (3 storage nodes)
├── docker/pki/                 # per-node mTLS certificates
├── scripts/                    # bootstrap-fleet, rolling-update, E2E
└── OpenGitBase.sln
```

## Stacks

| Stack | Paths | Guide |
|-------|-------|-------|
| Backend (.NET) | `applications/OpenGitBase.Api/`, `common/`, `features/` | [backend.md](backend.md) |
| Web UI (Nuxt) | `applications/opengitbase-web/` | [opengitbase-web/README.md](../applications/opengitbase-web/README.md) |
| Storage runtime | `applications/repo-storage-layer/` | [docs/prd/ha-storage-replication.md](../docs/prd/ha-storage-replication.md) |
| Git proxy | `applications/OpenGitBase.Dispatcher/` | [docs/prd/git-storage-proxy.md](../docs/prd/git-storage-proxy.md) |

## Feature organization

- **Backend features** live under `features/{feature-name}/` with Contracts, Feature, and Tests projects (see [backend.md](backend.md)).
- **Web UI** is organized under `applications/opengitbase-web/app/` (pages, components, composables, utils).
- Use `agentGenCli new backend-feature …` to add backend features so registration stays consistent.
