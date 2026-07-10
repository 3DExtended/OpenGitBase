# Read/write routing split

## Metadata

- ID: ers-06
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Split read and write routing to dedicated replica roles across dispatcher, API access-check, and web content paths.

**Writes:** always route to write primary.

**Reads:** route to read replica when healthy and sufficiently in sync; fallback to primary if read replica unhealthy or lagging beyond threshold.

**Access-check response** exposes `{ primary, readReplica, replicationEpoch, writeQuorumAvailable }`. Encrypted replicas never appear in routing targets.

Update web content reads to target read replica via existing content client pattern. Preserve lag banner behavior when read replica trails primary watermark.

## Acceptance criteria

- [ ] Dispatcher routes git push to primary only
- [ ] Dispatcher routes git fetch/clone to read replica; falls back to primary when read unavailable
- [ ] Access-check response includes read replica address separate from primary
- [ ] Web content API reads from read replica; shows sync banner when lagging
- [ ] Encrypted replica nodes never returned as read or write targets
- [ ] Routing handler tests cover read fallback, write-only primary, and encrypted exclusion
- [ ] HTTPS and SSH git paths share identical routing semantics

## Blocked by

- [04-four-copy-repository-create.md](./04-four-copy-repository-create.md)

## User stories covered

- 12 — As a developer cloning or fetching, I want read operations routed to the dedicated read replica, so that read load is separated from write traffic.
- 13 — As a developer browsing repository content on the web, I want reads served from the read replica, so that web traffic does not add load to the write primary.
- 14 — As a developer fetching after a successful push, I want to see pushed commits from the read replica once it catches up, so that normal workflows remain transparent.
- 16 — As a dispatcher, I want the access-check response to include the primary address and read replica address, so that I can route reads and writes without additional API round-trips.
- 57 — As a developer using git over HTTPS, I want the same primary/read routing and encrypted quorum write behavior as SSH, so that transport choice does not weaken replication guarantees.
- 58 — As a web user browsing a repository, I want content reads served from the read replica with a lag banner when appropriate, so that browsing behavior matches today's replica routing semantics.

## Notes

Can ship in parallel with issue 05 if create (04) is done; both depend on four-copy roles existing but not on each other.
