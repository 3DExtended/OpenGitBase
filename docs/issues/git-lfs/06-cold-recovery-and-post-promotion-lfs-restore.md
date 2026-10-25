<!-- forge: #240 -->

# [slice] lfs-06 — Cold recovery and post-promotion LFS restore

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

Losing both plaintext copies loses no LFS content, and a failover restores LFS availability quickly.

1. **Cold recovery (`ColdRecoveryService`).**
   - After the bare repository is reconstructed and verified, restore all of the repository's LFS objects from an encrypted replica: stream the ciphertext, decrypt in the API, verify the oid, and stream the plaintext to the new primary and read replica.
   - If an object fails to decrypt or verify, try the other encrypted replica.
   - The repository becomes writable as soon as git is restored. The LFS restore continues in the background, because the encrypted copies stay intact meanwhile.
   - Restore progress (objects restored / total) is recorded so the admin dashboard (lfs-08) can show it.
2. **Hot promotion.** After a read replica is promoted to primary, schedule an immediate LFS repair for the repository (the lfs-05 path), so objects the old read replica never received are restored with priority.
3. **Downloads during restore.** Objects not restored yet answer `503` with `Retry-After` (lfs-05 behavior). Objects already restored download normally.

Demo: push LFS files, stop and wipe the node holding primary + read replica, let cold recovery run. Clone with `git lfs pull` — the content is byte-identical.

## Acceptance criteria

- [ ] After cold recovery every recorded LFS object is present on the new plaintext nodes, verified against its oid
- [ ] A corrupted encrypted copy on one replica is detected, and the object is restored from the other replica
- [ ] The repository accepts git pushes before the LFS restore finishes; downloads of pending objects answer `503` + `Retry-After`
- [ ] Hot promotion schedules an LFS repair, and objects missing on the promoted node are restored
- [ ] Restore progress is recorded per repository
- [ ] Interrupted restores resume safely; repeated recovery attempts are idempotent
- [ ] `ColdRecoveryService` / promotion tests cover the above

## Blocked by

- #239

## User stories covered

- 30, 31 (after recovery and promotion)
