# Dispatcher git proxy (read + write)

**Type:** AFK  
**User stories:** 1, 2, 3, 6, 8, 9, 15, 16, 21, 22, 23, 24, 25, 30

## Parent

[PRD: Git Storage Proxy & Multi-Node Storage](../../prd/git-storage-proxy.md)

## What to build

Replace the dispatcher's "access allowed → exit 0" stub with a full git wire-protocol proxy.

After a successful repository access check, the dispatcher opens an SSH session to the assigned storage node using a **dispatcher-only internal private key** (not the user's key). It executes `git-upload-pack` or `git-receive-pack` with the **canonical `PhysicalPath`** from the access-check response, and pipes stdin/stdout/stderr between the client SSH session and the storage SSH session.

Extend the access-check response with routing fields when access is allowed:

```
physicalPath: "/srv/git/{repositoryId}.git"
storageNodeInternalHost: string
storageNodeInternalSshPort: int
```

When access would be allowed but the assigned storage node is unhealthy, return `allowed: false` with a storage-unavailability reason.

Storage nodes accept the dispatcher SSH key only; they do not validate user keys or call the access-check API during git operations.

## Acceptance criteria

- [ ] Access-check response includes `physicalPath`, storage node host, and SSH port when allowed
- [ ] Access-check denies with a clear reason when the assigned storage node is unhealthy
- [ ] Dispatcher proxies `git-upload-pack` (clone, fetch, pull) end-to-end through port 2223 (or configured dispatcher port)
- [ ] Dispatcher proxies `git-receive-pack` (push) end-to-end for users with write access
- [ ] Dispatcher uses canonical physical path from access check, not the client `{owner}/{slug}` path
- [ ] Push is rejected at the dispatcher when access check denies write access (before storage is contacted)
- [ ] Dispatcher propagates storage exit codes; client sees normal git success/failure behavior
- [ ] Storage `authorized_keys` contains only the dispatcher public key
- [ ] Access-check controller tests cover routing fields and unhealthy-node denial
- [ ] Dispatcher unit tests cover command/path mapping for proxy invocation
- [ ] Manual verification against a repo created via issue 03: `git clone`, `git pull`, and `git push` over `ssh://git@localhost:2223/{owner}/{repo}`
- [ ] Organization-owned repo (`{orgSlug}/{repoSlug}`) works if owner-slug resolution already supports orgs

## Blocked by

- [03-repository-create-delete-with-storage-assignment.md](./03-repository-create-delete-with-storage-assignment.md)
