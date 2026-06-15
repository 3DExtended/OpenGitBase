# Git storage proxy — implementation issues

Vertical slices for [PRD: Git Storage Proxy & Multi-Node Storage](../../prd/git-storage-proxy.md).

Implement in order; each issue is blocked by the one above it.

| # | Issue | Type | Blocked by |
|---|-------|------|------------|
| 1 | [Storage node registry and heartbeat](./01-storage-node-registry-and-heartbeat.md) | AFK | — |
| 2 | [Storage internal HTTP lifecycle API](./02-storage-internal-http-lifecycle-api.md) | AFK | 1 |
| 3 | [Repository create/delete with storage assignment](./03-repository-create-delete-with-storage-assignment.md) | AFK | 2 |
| 4 | [Dispatcher git proxy (read + write)](./04-dispatcher-git-proxy.md) | AFK | 3 |
| 5 | [Network isolation and e2e integration tests](./05-network-isolation-and-e2e-integration-tests.md) | AFK | 4 |
