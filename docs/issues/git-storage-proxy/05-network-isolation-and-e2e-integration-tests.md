# Network isolation and end-to-end integration tests

**Type:** AFK  
**User stories:** 26, 27, 31, 32

## Parent

[PRD: Git Storage Proxy & Multi-Node Storage](../../prd/git-storage-proxy.md)

## What to build

Harden the Docker deployment so storage nodes are not publicly reachable for git operations, and add automated tests that cover the full dispatcher → API → storage path.

**Networking:** storage services use the Compose internal network only; remove host port mappings for storage SSH in the default compose profile. Optional dev-only port mappings may remain behind a profile or documented override for debugging. Dispatchers remain the sole public SSH entry point for git clients.

**Integration tests:**
- Extend `repo-storage-layer` integration coverage for internal HTTP provision/delete (if not fully covered in issue 02)
- Add an end-to-end test that exercises `git push` and `git clone` through a dispatcher against a repository created via the API, with a registered storage node

Local `docker compose up` continues to work with the default small storage fleet (e.g. two nodes) without requiring a parametric compose generator.

## Acceptance criteria

- [ ] Storage services are not exposed on host ports in the default `docker compose` configuration
- [ ] Git operations (`clone`, `pull`, `push`) succeed via dispatcher host ports only
- [ ] Direct SSH to a storage node from the host fails or is unreachable in the default compose setup
- [ ] Dispatchers can still reach storage nodes on the internal network
- [ ] End-to-end integration test (script or automated test project) covers: register storage → create repo via API → push → clone via dispatcher
- [ ] Integration test runs in CI or is documented as a required pre-merge check for this feature area
- [ ] `docker compose up` with default config starts api, web, dispatchers, storage nodes, and postgres without manual steps
- [ ] README or agent docs mention how to enable optional storage host ports for local debugging

## Blocked by

- [04-dispatcher-git-proxy.md](./04-dispatcher-git-proxy.md)
