<!-- forge: #243 -->

# [slice] lfs-09 — LFS chaos E2E and production rollout

## Metadata

- Type: HITL
- Status: ready

## Parent

PRD discussion #234

## What to build

Prove the durability guarantee end to end on the real stack, document operations, and switch LFS on in production. Enabling it in production is a human decision after the chaos suite passes — hence HITL.

1. **Chaos E2E** (`tests/OpenGitBase.E2E.Tests/HaChaos`, prior art `HaStorageChaosTests`, `LargeRepositoryReplicationTests`):
   - Primary+read node lost → cold recovery → clone + `git lfs pull` returns identical bytes.
   - Hot promotion → objects the old read replica lacked are restored; downloads recover.
   - Encrypted LFS directory deleted on one replica while the DB says healthy → reconciler restores it.
   - Read replica lacks an object → download falls back to the primary.
   - Large object (> 100 MB) end to end, exercising every streaming path.
   - Repository deleted → LFS directories gone on every node.
2. **Operations documentation** (`docs/PROJECT-STATE.md` and the deploy notes):
   - The LFS data flow and the four-copy layout for LFS.
   - Storage sizing: each object stored as plaintext on the primary/read node(s) and twice encrypted.
   - Rollout order: API → storage nodes one at a time, primary last → dispatchers → enable `Lfs:Enabled`. Rollback in reverse, disabling first.
   - HAProxy timeouts vs `Lfs:MaxObjectBytes`.
   - SSH users: set `lfs.url` to the HTTPS endpoint (`https://<host>/<owner>/<repo>.git/info/lfs`).
3. **Production rollout (HITL):** deploy per the documented order, enable `Lfs:Enabled`, then run a smoke push/clone with a real LFS file against production.

## Acceptance criteria

- [ ] All chaos scenarios above pass in the compose E2E suite
- [ ] Docs describe data flow, sizing, rollout and rollback order, timeouts and SSH guidance
- [ ] Production deployed in the documented order; `Lfs:Enabled` switched on by a human after review
- [ ] Production smoke test: LFS push and clone round trip byte-identical

## Blocked by

- #237
- #238
- #239
- #240
- #241
- #242

## User stories covered

- 15, 48, 49, 51
