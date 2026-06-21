# Storage peer mTLS replication

## Metadata

- ID: ha-storage-03
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Enable storage nodes to replicate bare repository data to each other using git-native sync over **mTLS**, authenticated with the existing per-node PKI certificates. A source node can sync a repository at a known physical path to a peer; the peer applies the update, fsyncs, and reports success. Prefer reusing internal git smart HTTP infrastructure with mutual certificate verification rather than widening the dispatcher SSH trust boundary.

This slice delivers peer sync in isolation — not yet wired into push ack or watermark commit. Verifiable by provisioning a repo on one node, syncing to a second, and confirming matching refs/objects.

## Acceptance criteria

- [ ] Storage nodes can initiate git-native sync of a bare repo to a peer over mTLS
- [ ] Peer connections validate client and server certificates against fleet PKI (reject unknown certs)
- [ ] Sync is scoped to known physical paths — no arbitrary repository access
- [ ] After sync, the peer bare repo contains the same ref tips as the source for the synced state
- [ ] Sync failure returns structured error without partial ref corruption (idempotent or rollback-safe retry)
- [ ] Storage-layer integration test covers provision on node A → sync to node B → verify refs match via `git` commands
- [ ] Sync does not require dispatcher SSH identity or user credentials

## Blocked by

- [02-replica-set-schema-and-quorum-create.md](./02-replica-set-schema-and-quorum-create.md)

## User stories covered

- 6 — As a security reviewer, I want storage-to-storage replication authenticated with per-node mTLS using the existing PKI, so that peer sync does not widen the dispatcher SSH trust boundary.
- 7 — As the system, I want peer replication to use git-native mechanisms (internal fetch/sync of bare repos), so that every copy remains a valid bare repository on disk.

## Notes

- Exact transport (internal git smart HTTP port vs dedicated replication listener) may follow PRD assumption: reuse `STORAGE_INTERNAL_GIT_HTTP_PORT` with peer cert verification.
- Watermark reporting in heartbeat lands in slice 04.
