# Repository create/delete with storage assignment

**Type:** AFK  
**User stories:** 4, 5, 7, 13, 14, 19

## Parent

[PRD: Git Storage Proxy & Multi-Node Storage](../../prd/git-storage-proxy.md)

## What to build

Repositories are pinned to a storage node for their lifetime. Extend the repository model with `StorageNodeId` and adopt the canonical physical path convention `/srv/git/{repositoryId}.git` (stored in `PhysicalPath`).

**Repository create:** select the healthy storage node with the most `FreeBytesAvailable`, call the storage provisioner synchronously, then persist the repository row with `StorageNodeId` and `PhysicalPath`. If no healthy node exists or provisioning fails, the create request fails and no orphaned DB row is left behind.

**Repository delete:** resolve the assigned storage node, call the storage deleter synchronously, then remove the repository from the database (matching existing delete semantics in the product).

The **Storage Node Selection** module is a pure function: given healthy nodes, return the one with maximum free disk. Slug-based client URLs are unchanged; slug resolution remains in the API, not on storage.

## Acceptance criteria

- [ ] Repository entity migration adds `StorageNodeId` (required for new repos)
- [ ] New repositories set `PhysicalPath` to `/srv/git/{repositoryId}.git`
- [ ] Create repository selects the healthy node with the most free disk space
- [ ] Create repository calls storage provision synchronously before returning success
- [ ] Create repository fails with a clear error when no healthy storage node is available
- [ ] Create repository fails with a clear error when storage provisioning fails (no orphan DB row)
- [ ] Delete repository removes the bare repo from the assigned storage node synchronously before (or as part of) DB deletion
- [ ] Delete repository handles missing/unhealthy storage node with a clear failure (fail fast, no silent orphan)
- [ ] Storage node selection algorithm has dedicated unit tests
- [ ] Repository create/delete handler or controller tests cover success, no-healthy-nodes, and provision-failure paths
- [ ] Manual verification: create repo via API/UI → bare repo exists on the assigned node's `/srv/git` volume; delete → gone from disk and DB

## Blocked by

- [02-storage-internal-http-lifecycle-api.md](./02-storage-internal-http-lifecycle-api.md)
