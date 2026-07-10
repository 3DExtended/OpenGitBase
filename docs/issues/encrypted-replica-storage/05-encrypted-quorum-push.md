# Encrypted quorum push

## Metadata

- ID: ers-05
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Replace git-sync quorum replication to encrypted nodes with encrypted artifact upload, while keeping watermark and epoch semantics.

**Post-receive flow on primary:**

1. Fetch replication context from API
2. Request ephemeral Repository Key
3. Create git bundle, encrypt with AEAD, upload to at least one encrypted replica
4. Call API quorum-replicate with encrypted confirmation
5. Async: git fetch sync to read replica; upload to second encrypted replica

**API quorum-replicate:**

- Validate caller is primary
- Require encrypted artifact confirmation at `PrimaryWatermark + 1` from ≥1 encrypted replica
- Commit watermark atomically with epoch guard
- Quorum members: primary + ≥1 encrypted replica (not read replica)
- Roll back primary local watermark on failure

## Acceptance criteria

- [ ] Push succeeds only after primary and at least one encrypted replica confirm artifact receipt
- [ ] Push fails clearly when encrypted quorum unavailable; primary rolls back local watermark
- [ ] Read replica sync runs async and does not block push acknowledgment
- [ ] Second encrypted replica receives artifact async after quorum commit
- [ ] Watermark increments by exactly 1 per successful push; epoch stale rejection unchanged
- [ ] Integration tests cover happy path, encrypted node down, corrupt upload rejection, and async catch-up
- [ ] Existing post-receive hook integration preserved for primary-only git apply

## Blocked by

- [04-four-copy-repository-create.md](./04-four-copy-repository-create.md)

## User stories covered

- 1 — As a repository owner, I want my repository replicated without exposing plaintext data to untrusted storage nodes, so that third-party operators cannot read my code.
- 2 — As a repository owner, I want push to succeed only after at least one encrypted replica confirms receipt, so that acknowledged pushes survive loss of the primary+read node pair.
- 3 — As the system, I want the read replica and second encrypted replica to catch up asynchronously after quorum replication, so that push latency is not blocked on all four copies.
- 4 — As the system, I want each repository to maintain a monotonic replication watermark tied to encrypted artifact confirmation, so that in-sync status and recovery decisions use an exact signal.
- 5 — As the system, I want the primary to commit watermarks only after encrypted quorum replication succeeds, so that watermarks always represent durably replicated state.
- 11 — As a developer pushing changes, I want write operations routed exclusively to the write primary, so that replication orchestration has a single writer.
- 15 — As a git client, I want push to fail clearly when encrypted quorum cannot be met, so that I know the system refused to ack without durability rather than losing data silently.

## Notes

v1 produces full bundle per watermark (not incremental packfiles). Read replica git sync reuses existing mTLS peer fetch from primary.
