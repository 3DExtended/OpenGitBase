# Git LFS — slices

Parent PRD: [#234](../../prd/git-lfs.md)

Vertical tracer-bullet slices. `Lfs:Enabled` stays off in production until lfs-09.

| ID | Title | Type | Status | Blocked by | Forge |
|----|-------|------|--------|------------|-------|
| lfs-01 | [Tracer bullet: git lfs push and clone round trip](01-tracer-bullet-git-lfs-push-and-clone-round-trip.md) | AFK | ready | — | [#235](https://api.opengitbase.com/opengitbase/open-git-base/discussions/235) |
| lfs-02 | [Permissions, size limits, batch cap and quota](02-permissions-size-limits-batch-cap-and-quota.md) | AFK | ready | lfs-01 (#235) | [#236](https://api.opengitbase.com/opengitbase/open-git-base/discussions/236) |
| lfs-03 | [Encrypted quorum on verify](03-encrypted-quorum-on-verify.md) | AFK | ready | lfs-01 (#235) | [#237](https://api.opengitbase.com/opengitbase/open-git-base/discussions/237) |
| lfs-04 | [Pre-receive integrity gate for LFS pointers](04-pre-receive-integrity-gate-for-lfs-pointers.md) | AFK | ready | lfs-01 (#235) | [#238](https://api.opengitbase.com/opengitbase/open-git-base/discussions/238) |
| lfs-05 | [Reconciler repair from on-disk LFS inventory](05-reconciler-repair-from-on-disk-lfs-inventory.md) | AFK | ready | lfs-03 (#237) | [#239](https://api.opengitbase.com/opengitbase/open-git-base/discussions/239) |
| lfs-06 | [Cold recovery and post-promotion LFS restore](06-cold-recovery-and-post-promotion-lfs-restore.md) | AFK | ready | lfs-05 (#239) | [#240](https://api.opengitbase.com/opengitbase/open-git-base/discussions/240) |
| lfs-07 | [Web UI: LFS files in blob view and raw downloads](07-web-ui-lfs-files-in-blob-view-and-raw-downloads.md) | AFK | ready | lfs-01 (#235), lfs-02 (#236) | [#241](https://api.opengitbase.com/opengitbase/open-git-base/discussions/241) |
| lfs-08 | [Repository deletion cleanup and admin LFS figures](08-repository-deletion-cleanup-and-admin-lfs-figures.md) | AFK | ready | lfs-05 (#239) | [#242](https://api.opengitbase.com/opengitbase/open-git-base/discussions/242) |
| lfs-09 | [LFS chaos E2E and production rollout](09-lfs-chaos-e2e-and-production-rollout.md) | HITL | ready | lfs-03 (#237), lfs-04 (#238), lfs-05 (#239), lfs-06 (#240), lfs-07 (#241), lfs-08 (#242) | [#243](https://api.opengitbase.com/opengitbase/open-git-base/discussions/243) |
