# Storage internal HTTP lifecycle API

**Type:** AFK  
**User stories:** 17, 18 (provision), 20 (delete, partial)

## Parent

[PRD: Git Storage Proxy & Multi-Node Storage](../../prd/git-storage-proxy.md)

## What to build

Storage nodes expose a private internal HTTP API on the Docker network, authenticated with the per-node bearer token issued during registration. The API includes:

- **Provision** — create a bare git repository at a given physical path (e.g. `/srv/git/{repositoryId}.git`) via `git init --bare`
- **Delete** — remove a bare repository at a given physical path

Requests without a valid `Authorization: Bearer` token for that node are rejected. The API-side **Storage Provisioner Client** can call these endpoints using the stored per-node token.

This slice wires provisioning and deletion on storage and proves the API can reach a node over the internal network. It does not yet hook into repository create/delete in the product or git proxying.

## Acceptance criteria

- [ ] Storage container runs an internal HTTP server bound to the private network
- [ ] `POST` provision endpoint creates a bare repo at the requested path; returns error if path is invalid or already exists
- [ ] `DELETE` endpoint removes the bare repo at the requested path; returns appropriate error if not found
- [ ] Endpoints reject requests without a valid per-node bearer token
- [ ] API-side provisioner client can provision and delete a repo on a registered, healthy node
- [ ] Provisioner client tests cover success and auth-failure cases (mocked HTTP)
- [ ] Storage-layer integration test (or new script) verifies provision + delete over HTTP with token auth
- [ ] Provisioned bare repo is usable by `git-upload-pack` / `git-receive-pack` when invoked directly on storage (sanity check)

## Blocked by

- [01-storage-node-registry-and-heartbeat.md](./01-storage-node-registry-and-heartbeat.md)
