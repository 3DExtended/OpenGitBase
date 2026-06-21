# Quorum push and watermark commit

## Metadata

- ID: ha-storage-04
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Wire peer replication into the git push path with **quorum 2/3** durability. After the primary completes `git-receive-pack`, it synchronously replicates to at least one peer via mTLS git-native sync; only when two nodes (including primary) have durably applied the update does the primary call the API to bump `PrimaryWatermark`. The third replica catches up asynchronously afterward. Extend storage heartbeats to report per-repo `AppliedWatermark` values.

Push must **fail** (no watermark bump) when fewer than two trio members are reachable. Stale-epoch or non-primary watermark commits are rejected by the API.

## Acceptance criteria

- [ ] Successful push increments `PrimaryWatermark` only after primary and at least one replica confirm durable apply
- [ ] Third replica receives async catch-up after client ack without blocking push latency on all three
- [ ] Push fails without watermark bump when only one trio member is reachable
- [ ] Push succeeds when one replica is down but primary and another replica are healthy (quorum 2/3)
- [ ] API rejects watermark commit from non-primary or stale `ReplicationEpoch`
- [ ] Storage heartbeat payload includes per-repo applied watermarks (or equivalent lag signal)
- [ ] `RepositoryReplica.IsInSync` derives correctly when `AppliedWatermark == PrimaryWatermark`
- [ ] Integration test: push through dispatcher updates watermarks on two nodes; third may lag briefly
- [ ] Integration test: push with two nodes down fails cleanly

## Blocked by

- [03-storage-peer-mtls-replication.md](./03-storage-peer-mtls-replication.md)

## User stories covered

- 1 — As a repository owner, I want my repository data stored on three independent storage nodes, so that the permanent loss of any single node does not destroy my repository.
- 2 — As a git client pushing changes, I want push to succeed only after at least two of three replicas have durably stored the new objects and refs, so that an acknowledged push cannot be lost to a single-node failure.
- 3 — As the system, I want the third replica to catch up asynchronously after a quorum write, so that push latency is not blocked on all three nodes while RF=3 is eventually restored.
- 4 — As the system, I want each repository to maintain a monotonic replication watermark, so that in-sync status and promotion decisions are based on an exact signal rather than time estimates.
- 5 — As the system, I want the primary to bump the watermark only after quorum replication succeeds, so that watermarks always represent durably replicated state.
- 17 — As a git client, I want push to fail clearly when fewer than two replicas are reachable, so that I know the system refused to ack without quorum rather than losing data silently.
- 32 — As a storage node, I want to report per-repository applied watermarks in heartbeat payloads, so that the API can mark replicas in-sync for read routing and detect lag.

## Notes

- Dispatcher read/write split is slice 05; this slice may still route all git ops to primary only until then.
- Push latency increase from synchronous peer sync is expected and acceptable per PRD.
