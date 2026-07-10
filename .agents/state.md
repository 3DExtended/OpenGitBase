# Project state

Manually maintained snapshot of the running project. The agentGenCli manifest in [`.agentGenCli.json`](../.agentGenCli.json) only records the original scaffold; see [docs/PROJECT-STATE.md](../docs/PROJECT-STATE.md) for the full architecture overview.

- Project: OpenGitBase
- Initialized: 2026-06-10 (agentGenCli)
- Backend: dotnet
- Frontend: Nuxt 4 SPA (`applications/opengitbase-web/`) — not agentGenCli Flutter

## Runtime applications

| App | Path | Role |
|-----|------|------|
| API | `applications/OpenGitBase.Api/` | Control plane, REST, replication orchestration |
| Dispatcher | `applications/OpenGitBase.Dispatcher/` | Git Smart HTTP / SSH proxy |
| Storage node | `applications/repo-storage-layer/` | Bare git repos, RF=3 replication |
| Web UI | `applications/opengitbase-web/` | Nuxt SPA |

## Init modules (scaffold)

- Email (SendGrid): yes
- Auth (Users + JWT): yes

## Enabled stacks

- Backend guide: [backend.md](backend.md)
- Web frontend: [applications/opengitbase-web/README.md](../applications/opengitbase-web/README.md) (no `.agents/frontend.md` — that guide is for Flutter)

## Backend features

| Feature | Database | API | Notes |
|---------|----------|-----|-------|
| Users | yes | auth routes | Email encrypted at rest |
| Repository | yes | yes | RF=3 HA provisioning |
| RepositoryMember | yes | yes | |
| Organization | yes | yes | Invites |
| StorageNode | yes | `/api/v1/*` internal | Fleet registry, no public CRUD |
| GitAccessToken | yes | yes | HTTPS git PATs |
| PublicGitSshKey | yes | yes | SSH git keys |
| Discussion | yes | yes | Threaded, anchored comments |
| MergeRequest | yes | yes | Server-side merge |

## Local stack

- Compose: 2× API, 2× web, 3× storage, 2× dispatcher, Postgres, Redis, HAProxy
- Entry: `http://localhost:8089`
- Bootstrap: `scripts/bootstrap-fleet.sh` (PKI, enrollments, fleet SSH keys)

## Documentation entry points

- [docs/PROJECT-STATE.md](../docs/PROJECT-STATE.md) — what is implemented, component interactions, encryption posture
- [docs/prd/](../docs/prd/) — feature and architecture PRDs
- [docs/issues/](../docs/issues/) — implementation slices
