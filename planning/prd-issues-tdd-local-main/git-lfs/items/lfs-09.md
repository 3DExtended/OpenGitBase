# lfs-09 — LFS chaos E2E and production rollout

## Summary

- **Cold recovery fixes.** Writing the chaos scenario showed two gaps that kept the LFS restore after cold recovery
  (lfs-06) from working in a common layout.
  - **Target choice.** `ColdRecoveryService` picked the first healthy node not named storage-2 or storage-3.
    Encrypted placement follows free space, so a repository's encrypted copies can sit on storage-4. The recovery
    then imported git onto its own encrypted replica, and one encrypted copy was left.
    - It now prefers a healthy node that holds no encrypted copy of the repository, and among those, nodes outside
      the platform's encrypted layout.
    - Only when every healthy node holds an encrypted copy does one of them become the primary.
  - **Node role.** `POST /internal/repos/import-bundle` did not touch the node's role file. A node that had been the
    repository's encrypted replica kept refusing plaintext LFS (403) and git content after it became the primary.
    The import now records the node as the repository's `Primary`, since cold recovery is its only caller.
- **Chaos E2E** (`tests/OpenGitBase.E2E.Tests`):
  - `HaChaos/LfsColdRecoveryChaosTests` (new, FullHa):
    - storage-1 takes no new repositories while the test repository is created (admin capacity cap, restored at
      once), so stopping its primary does not cold-recover every repository on the stack;
    - the primary loses its LFS data and is stopped; the test marks stale nodes via the fleet drift report;
    - cold recovery must move the primary to the node without a copy and keep both encrypted replicas;
    - a clone must return the object byte-identical, and it must be decrypted onto the new primary;
    - the old primary returns without the object, and a second clone must fall back to the new primary.
  - `GitHttps/LfsLargeObjectTests` (new): a 120 MiB object through the dispatcher upload, verify with encryption, the
    copies (each encrypted copy checked for its exact AEAD size), a clone, and the web raw download through the API.
  - Scenarios covered by earlier slices: the encrypted LFS directory deleted while the database says healthy
    (`LfsReconcilerRepairTests`, lfs-05), and repository deletion (`LfsRepositoryDeletionTests`, lfs-08).
  - **Hot promotion is not reachable in compose.** The planner always places the read replica on the primary's node
    (`ReplicaSetPlanner`), so no repository has a separate `ReadReplica` row, and failover can only cold-recover.
    The promotion path is covered by `PromotePrimaryReplicaQueryHandlerTests` (lfs-06).
- **Operations docs.**
  - `docs/deployment/git-lfs-operations.md` covers:
    - data flow and the four-copy layout with paths and volumes;
    - sizing (3 × S with primary and read replica on one node, 4 × S otherwise);
    - configuration, and the limits along the request path;
    - rollout and rollback order, the smoke test, SSH `lfs.url`, health and repair, and troubleshooting.
  - `docs/PROJECT-STATE.md` lists LFS in the storage table, the encryption posture and the doc index.
  - **Finding for the rollout.** Production goes through the Cloudflare tunnel. Proxied hostnames accept request
    bodies up to 100 MB on Free/Pro (200 MB Business) and wait at most 125 s for the origin.
    - With the 2 GiB `Lfs:MaxObjectBytes` default, a larger upload passes the batch and then fails at Cloudflare
      with an HTML 413.
    - Before enabling, either set `Lfs__MaxObjectBytes` (for example 100 MiB) or serve git on a DNS-only hostname.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-09` (forge #243)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-03, lfs-04, lfs-05, lfs-06, lfs-07, lfs-08

### Full chain

`lfs-01 -> lfs-02 -> … -> lfs-08 -> lfs-09`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - Storage, `python3 -m unittest discover`: 168 ran. The one error is the existing `test_storage_artifact_crypto`
    import. New: `test_importing_a_bundle_makes_a_former_encrypted_replica_the_plaintext_primary`.
  - `dotnet test tests/OpenGitBase.Api.Tests`: 888 passed. New: `ColdRecoveryServiceTests`
    `TryRecoverAsync_PrefersANodeWithoutACopyOverOneOfTheEncryptedReplicas`. A mutation check (the old target choice)
    fails it.
  - E2E meta tests (`RequiresCompose!=true`): 29 passed.
- **Compose E2E: passed.**
  - **Docker trouble during the slice.** Docker Desktop on this host hung for a while:
    - image builds and `docker pull` stopped at registry metadata, for other projects' builds too;
    - containers were SIGKILLed in a loop, and api-1 hung after a start-up migration failure while Postgres
      recovered.

    After recreating `ssh-lb` and restarting api-1, the stack was stable. Builds still hung, so the new code went
    into the running containers directly:
    - storage: `storage-http-server.py` and `storage_lfs.py` via `docker cp`, restarting the nodes one at a time;
    - API: the 30 `OpenGitBase.*.dll` from a local `dotnet publish -c Release`. All third-party assemblies were
      byte-identical to the image's.

    Recreating those containers brings back the lfs-07 image until the images are rebuilt.
  - `GitHttps.Lfs*`: 9/9 passed. That covers lfs-01..08, plus the new `LfsRepositoryDeletionTests` and
    `LfsLargeObjectTests`, the 120 MiB object at 22 s.
  - `HaChaos.LfsColdRecoveryChaosTests` (`OPENGITBASE_E2E_COMPOSE_PROFILE=FullHa`) passed in 2 m 10 s. Afterwards
    all four nodes were healthy and storage-1's capacity was back to unlimited.
- **Production: live since 2026-10-18.**
  - Decision: `Lfs:MaxObjectBytes` = 100 MiB (`104857600`), because of the Cloudflare body limit on proxied
    hostnames; no DNS-only hostname.
  - Deployed `e372f84` (this branch merged with `main`) in the documented order:
    - `remote-rolling-update.sh` for API and web with LFS off; the migrations `AddLfsObjects` and
      `AddLfsObjectCopies` were applied;
    - `--full` for storage 2, 3, 4, 1 and the dispatchers (10 m 26 s, no restarts).
  - Enabled with `Lfs__Enabled: "true"` and `Lfs__MaxObjectBytes: "104857600"` in the `x-api-env` block of the
    Tower override (backup `opengitbase-secrets/docker-compose.override.yml.pre-lfs`), then `api-2` and `api-1`
    recreated one at a time. The batch endpoint answered `501` before and `401` (credentials needed, as git over
    HTTPS) after.
  - Smoke test on `admin/lostest` through `api.opengitbase.com`:
    - a 5,000,000-byte object pushed and cloned back byte-identical;
    - one `LfsObject` row and three copies: plaintext on storage-1 (primary), encrypted on storage-2 and
      storage-3 at 5,000,109 bytes each (29 + size + 5 × 16);
    - `StorageBytesUsed` 5,026,498 = 5,000,000 LFS + 26,498 git.
  - **Follow-up: the locking hint.** `git push` printed "Locking support detected on remote" on every push. The
    `locks/verify` stub answered `200` with no locks, and user story 13 wants no locking hint at all.
    - Probe with git-lfs 3.7.1 against a local LFS server: on `200` the hint comes on every push; on `404` or `501`
      git-lfs writes `lfs.<url>.locksverify false` once and pushes without a hint.
    - The dispatcher now answers `501` "Locking is not supported." (`LfsHandlerTests`
      `LocksVerify_Answers501SoGitLfsTurnsLockVerificationOffQuietly`; it fails against the old stub).
  - **Incident during the `--full` roll: open-git-base unpushable (`403`).**
    - Chain:
      1. The database stalled for about 95 s (11:31–11:33 UTC). No heartbeat arrived for 105 s, above
         `MissedHeartbeatThresholdSeconds` (90).
      2. When the database came back, storage-4 had reported again and the other three nodes were flagged unhealthy.
      3. Failover cold-recovered open-git-base from its working primary (storage-1) onto storage-4, which held one
         of its encrypted copies.
      4. `import-bundle` cloned the bundle with a working tree, and storage-4 still ran the old image, which left its
         role file at `EncryptedReplica`. git-http-backend answered `403`.
    - Repair: storage-1 made the primary again and storage-4 an encrypted replica, with epoch 12; the clone on
      storage-4 moved to `/var/lib/opengitbase/quarantine/`. The push of `a02ef53` then replicated, with watermark 87
      on storage-1 and the artifact on storage-2 and storage-4.
    - Fix 1, `import-bundle`:
      - It now restores a bare repository: `clone --mirror` keeps every ref, including `refs/internal/*`; the
        origin pointing at the temporary bundle is removed; `http.receivepack` is set.
      - With a working tree the hooks in `<repo>.git/hooks` never ran, so pushes to a cold-recovered primary were
        never replicated.
      - Tests: `test_importing_a_bundle_restores_a_bare_repository_that_takes_pushes`, red against the old clone.
        `LfsColdRecoveryChaosTests` now pushes after the recovery and waits for both encrypted replicas to hold the
        artifact (not yet run; the local compose stack was removed).
    - Fix 2, failover: before promoting or cold-recovering, `PromotePrimaryReplicaQueryHandler` asks the primary
      directly (`StorageNodeLivenessProbe`: a GET without a token, where any HTTP answer counts, with a 3 s
      timeout). A primary that answers stays the primary, and the skip is logged.
      - Tests: `RunQueryAsync_WhenTheUnhealthyPrimaryStillAnswers_KeepsItAsThePrimary` and `…_StartsNoColdRecovery`,
        both red without the guard, plus `StorageNodeLivenessProbeTests`.
    - Three more repositories were working-tree clones from earlier cold recoveries: `a1157eab…` (lifeChange) on
      storage-1, and `26d1a49d…` (notes) and `7805ee16…` (SocialPoster) on storage-3.
      - No push had landed in them since their import, so nothing was lost. lifeChange's second branch sat only
        under `refs/remotes/origin/`, hidden from clones.
      - All three were converted to bare repositories on 2026-10-18. lifeChange's original is kept in
        `/var/lib/opengitbase/quarantine/`, and the storage-3 originals in `/srv/git/.nonbare-backup-20261018-*`.
        Afterwards every repository on every node had `HEAD` on `main` and a `post-receive` hook where git runs it.
    - Rollout: API with the guard (`0c10f74`) at 13:30 UTC, then `--full` for the storage nodes (exit 0, no
      failover during the roll). Afterwards every repository's encrypted artifacts matched its primary watermark.
    - Follow-up from another session: a bundle carries no symref, so `clone --mirror` guesses `HEAD`, and git 2.47
      in the image can pick another branch on main's commit. `3158cf8` sets `HEAD` to `main` after the import. It
      reaches the storage nodes with the next `--full`.
- Commit(s): see `progress-log.md`
