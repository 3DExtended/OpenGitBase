<!-- forge: #153 -->

# Read/write routing (access check + dispatcher)

## Metadata

- ID: ha-storage-05
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Extend repository access-check responses and dispatcher routing so **writes always target the primary** and **reads may target the primary or any in-sync replica**. Access check returns primary address, an array of read-eligible targets (primary plus replicas where watermark matches), and `replicationEpoch`. Deny push when write quorum is unavailable (fewer than two healthy trio members). Apply the same routing to SSH and HTTPS git transports.

Dispatcher read selection: prefer primary; if load-spreading among replicas is implemented, use primary first then round-robin among in-sync replicas only.

## Acceptance criteria

- [ ] Access-check allowed response includes primary node routing fields and read-eligible targets based on in-sync status
- [ ] Access-check includes `replicationEpoch` for dispatcher/storage epoch validation
- [ ] Push (receive-pack) requests denied when fewer than two trio members are healthy, with clear reason
- [ ] Fetch/clone (upload-pack) allowed when at least one read-eligible target exists
- [ ] SSH dispatcher routes writes to primary and reads to primary or an in-sync replica
- [ ] HTTPS smart HTTP dispatcher applies identical read/write routing rules
- [ ] Client fetching after a quorum push sees pushed commits whether hitting primary or in-sync replica
- [ ] API controller tests cover read target inclusion/exclusion based on watermark lag
- [ ] Dispatcher unit tests cover primary-first read selection and write-always-primary behavior

## Blocked by

- [04-quorum-push-and-watermark-commit.md](./04-quorum-push-and-watermark-commit.md)

## User stories covered

- 13 — As a developer cloning or fetching, I want read operations routed to the primary or any in-sync replica, so that read load can spread without serving stale refs from a lagging replica.
- 14 — As a developer pushing, I want write operations always routed to the current primary, so that replication orchestration has a single writer.
- 15 — As a dispatcher, I want the access-check response to include the primary address and the set of read-eligible replica addresses, so that I can route reads and writes without additional API round-trips.
- 16 — As a git client fetching after a successful push, I want to see the pushed commits whether I hit the primary or an in-sync replica, so that read routing transparency holds for normal workflows.
- 38 — As a developer using git over HTTPS, I want the same primary/read-replica routing and quorum write behavior as SSH, so that transport choice does not weaken replication guarantees.

## Notes

- Lagging third replica must never appear in read-eligible targets.
- Failover routing updates are slice 06; this slice uses current primary from access check.
