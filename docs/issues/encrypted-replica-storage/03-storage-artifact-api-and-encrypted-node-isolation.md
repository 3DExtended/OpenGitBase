<!-- forge: #121 -->

# Storage artifact API and encrypted node isolation

## Metadata

- ID: ers-03
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Extend the storage layer with encrypted artifact storage and enforce that encrypted-replica nodes cannot serve Git.

**Artifact storage layout:**

```
{artifactRoot}/{repoId}/{watermark}/manifest.json
{artifactRoot}/{repoId}/{watermark}/bundle.aead
```

**Internal HTTP endpoints:**

- `PUT /internal/repos/{id}/artifacts/{watermark}` — receive manifest + encrypted bundle; authenticate via existing storage bearer token
- `GET /internal/repos/{id}/artifacts/{watermark}` — recovery fetch; restricted to API-initiated recovery callers
- `DELETE /internal/repos/{id}/artifacts/{watermark}` — delete quorum extension (wired fully in issue 09)

When a storage node's role for a repository is `EncryptedReplica`, reject git Smart HTTP, SSH git, and bare-repo content reads for that repository. Artifact endpoints remain available.

## Acceptance criteria

- [ ] PUT stores manifest and bundle atomically; returns success only after durable write
- [ ] GET returns stored artifact for valid watermark; 404 for missing artifact
- [ ] Encrypted-replica role rejects git upload-pack, receive-pack, and internal bare-repo content reads
- [ ] Primary and read-replica roles continue to serve git normally
- [ ] Storage integration tests cover upload, fetch, git rejection on encrypted role, and auth failure
- [ ] Artifact root path is configurable and separate from bare git root

## Blocked by

- [02-rf4-schema-keys-and-artifact-library.md](./02-rf4-schema-keys-and-artifact-library.md)

## User stories covered

- 6 — As a security reviewer, I want encrypted artifact upload authenticated with existing storage node credentials and mTLS, so that replication does not widen trust boundaries.
- 7 — As the system, I want read replica synchronization to use git-native fetch from the primary, so that the read copy remains a valid bare repository.
- 17 — As a storage node holding an encrypted replica, I want Git operations rejected on encrypted artifact storage, so that the node cannot accidentally serve repository contents.

## Notes

mTLS for artifact upload may reuse existing peer trust where applicable; primary uploads via internal HTTP with bearer token in v1.
