<!-- forge: #239 -->

# [slice] lfs-05 — Reconciler repair from on-disk LFS inventory

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

Missing LFS copies heal themselves. Decisions come from what is actually on each node's disk, not from what the database believes — the lesson of the false-healthy artifact loss fixed in #230.

1. **Inventory endpoint (storage).** Per repository, list the LFS objects on disk (oid + size), plaintext or encrypted by the node's role, on the internal HTTP port.
2. **Reconciler step "LFS copies"** in `AntiEntropyReconcilerService`, after the existing steps:
   - Fetch the inventory of every plaintext and encrypted node of each repository with LFS objects.
   - Compare it with the repository's `LfsObject` set. Update `LastVerifiedAt` for copies found; delete `LfsObjectCopy` rows for copies not found.
   - Repair, longest-missing first:
     - missing encrypted copy → re-encrypt from a plaintext copy;
     - missing plaintext copy → copy from the other plaintext node, or decrypt from an encrypted replica (in the API, oid verified) when no plaintext copy exists.
   - Each repository and each node fails on its own. A node that does not answer is skipped and its rows are **not** changed.
   - Log `LFS object <oid> of <repository> missing on <node>` and each repair result.
3. **New replicas.** A replica added by rebalance or RF4 backfill is filled by the same repair path; it needs no separate copy job.
4. **Recorded but unavailable.** When a download finds no read target holding a recorded object, the dispatcher answers `503` with `Retry-After` (not `404`), and the API schedules an immediate LFS repair for that repository (debounced).

Demo: delete the encrypted LFS directory of one repository on an encrypted replica while the database still lists its copies. Restart an API instance — the reconciler restores the copies. Repeat with the plaintext directory on the read replica.

## Acceptance criteria

- [ ] On-disk loss of encrypted copies is repaired while the DB still lists them (regression test in the style of #230)
- [ ] On-disk loss of plaintext copies is repaired from the other plaintext node, or from an encrypted replica when no plaintext copy exists
- [ ] Decrypted restores are verified against the oid before they are written
- [ ] An unreachable node does not block repairs on other nodes or repositories, and its rows stay unchanged
- [ ] A replica newly assigned by rebalance or backfill ends up with all LFS objects of the repository
- [ ] A download of a recorded object that no target holds answers `503` + `Retry-After` and triggers a repair
- [ ] Every repair and every missing copy is logged with repository, oid and node
- [ ] Reconciler service tests and storage inventory tests cover the above

## Blocked by

- #237

## User stories covered

- 26, 27, 28, 29, 31, 32, 33
