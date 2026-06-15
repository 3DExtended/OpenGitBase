# Storage node registry and heartbeat

**Type:** AFK  
**User stories:** 10, 11, 12, 28, 29

## Parent

[PRD: Git Storage Proxy & Multi-Node Storage](../../prd/git-storage-proxy.md)

## What to build

A storage node can join the OpenGitBase fleet without manual API configuration. On container startup, the storage agent registers with the API (node id, internal hostname, SSH port, HTTP port). The API persists a `StorageNode` record and issues a per-node API token (returned once at registration). The agent then sends periodic heartbeats reporting free and total bytes available on the `/srv/git` volume. The API updates disk stats and marks the node healthy on each successful heartbeat; after a configurable missed-heartbeat window, the node is marked unhealthy.

Expose API queries to list healthy storage nodes and fetch a node by id. This slice does not yet provision repositories or proxy git traffic — it establishes the control-plane registry that later slices depend on.

**Schema (conceptual):**

```
StorageNode {
  Id: Guid
  NodeId: string
  InternalHost: string
  InternalSshPort: int
  InternalHttpPort: int
  ApiTokenHash: string
  FreeBytesAvailable: long
  TotalBytesAvailable: long
  LastHeartbeatAt: DateTimeOffset
  IsHealthy: bool
  RegisteredAt: DateTimeOffset
}
```

## Acceptance criteria

- [ ] A new `StorageNode` entity and migration exist in the backend
- [ ] Storage agent registers with the API on startup and receives a per-node token
- [ ] Re-registration of the same `NodeId` is idempotent (updates host/ports, does not create duplicates)
- [ ] Storage agent sends heartbeats at a configured interval with free/total bytes for `/srv/git`
- [ ] API marks a node healthy on successful heartbeat and updates disk stats
- [ ] API marks a node unhealthy after the configured missed-heartbeat threshold
- [ ] API exposes a way to query healthy storage nodes (handler + endpoint or internal query usable by tests)
- [ ] Starting a storage container in `docker compose` results in a healthy node visible to the API within two heartbeat intervals
- [ ] Query handler tests cover registration and heartbeat state transitions
- [ ] Unhealthy nodes are excluded from "healthy nodes" queries

## Blocked by

None — can start immediately.
